using LibraryManagement.Domain;

namespace LibraryManagement.Application;

public record CheckoutResult(bool Success, string Message, decimal Amount, int? OrderId = null);

public interface ILibraryRepository
{
    List<Book> GetBooks();
    Book? GetBook(int id);
    List<BorrowOrder> GetOrders();
    List<AppUser> GetUsers();
    Coupon? FindCoupon(string code);
    void AddReview(BookReview review);
    void AddUser(AppUser user);
    void SaveOrder(BorrowOrder order);
    void SavePayment(Payment payment);
    void SaveNotification(Notification notification);
    void CreateBook(Book book);
    void UpdateBook(Book book);
    void DeleteBook(int bookId);
    void DeleteOrder(int orderId);
    List<Notification> GetNotifications(int userId);
    void MarkNotificationRead(int notificationId);
}

public interface INotificationSender
{
    void SendEmail(string toEmail, string subject, string body);
}

public class PricingService
{
    public decimal GetEffectiveBookPrice(Book book)
    {
        var percent = book.BorrowCount > 10 ? Math.Clamp(book.DiscountPercent, 5, 10) : 0;
        return book.BasePrice * (1 - percent / 100m);
    }
}

public class CheckoutService(PricingService pricingService, ILibraryRepository repository)
{
    public const decimal BuyMultiplier = 9.5m;

    public CheckoutResult Checkout(int userId, IEnumerable<(int bookId, int quantity, bool isPurchase)> items, string? couponCode)
    {
        var books = repository.GetBooks();
        var orderItems = new List<BorrowOrderItem>();
        decimal subtotal = 0;
        bool hasAnyBorrow = false;

        foreach (var item in items)
        {
            var book = books.FirstOrDefault(x => x.Id == item.bookId);
            if (book is null)
                return new CheckoutResult(false, $"Book #{item.bookId} not found.", 0);
            if (book.InventoryCount < item.quantity)
                return new CheckoutResult(false, $"Not enough inventory for {book.Name}.", 0);

            var borrowPrice = pricingService.GetEffectiveBookPrice(book);
            var unitPrice = item.isPurchase ? Math.Round(borrowPrice * BuyMultiplier, 0) : borrowPrice;
            subtotal += unitPrice * item.quantity;

            orderItems.Add(new BorrowOrderItem
            {
                BookId = book.Id,
                Quantity = item.quantity,
                UnitPrice = unitPrice,
                IsPurchase = item.isPurchase
            });

            book.InventoryCount -= item.quantity;
            if (item.isPurchase)
            {
                book.NumberSold += item.quantity;
            }
            else
            {
                book.BorrowCount += item.quantity;
                hasAnyBorrow = true;
            }
            repository.UpdateBook(book);
        }

        var coupon = string.IsNullOrWhiteSpace(couponCode) ? null : repository.FindCoupon(couponCode);
        var discount = coupon is { IsActive: true } ? subtotal * coupon.Percent / 100m : 0;
        var total = subtotal - discount;

        var order = new BorrowOrder
        {
            UserId = userId,
            BorrowDate = DateTime.UtcNow,
            DueDate = hasAnyBorrow ? DateTime.UtcNow.AddDays(14) : DateTime.UtcNow,
            Status = hasAnyBorrow ? BorrowStatus.Borrowing : BorrowStatus.Purchased,
            TotalAmount = total,
            Items = orderItems
        };

        repository.SaveOrder(order);
        repository.SavePayment(new Payment
        {
            BorrowOrderId = order.Id,
            Amount = total,
            CreatedAt = DateTime.UtcNow,
            Status = PaymentStatus.Paid
        });

        return new CheckoutResult(true, "Checkout successful.", total, order.Id);
    }
}

public class ReminderService(ILibraryRepository repository, INotificationSender notificationSender)
{
    public int ProcessDueReminders()
    {
        var users = repository.GetUsers();
        var orders = repository.GetOrders();
        var now = DateTime.UtcNow.Date;
        var reminderCount = 0;

        foreach (var order in orders.Where(x => x.Status is BorrowStatus.Borrowing or BorrowStatus.Overdue))
        {
            var user = users.FirstOrDefault(x => x.Id == order.UserId);
            if (user is null)
            {
                continue;
            }

            if (now > order.DueDate.Date)
            {
                var overdueDays = (now - order.DueDate.Date).Days;
                var lateFee = order.TotalAmount * 0.03m * overdueDays;
                order.LateFeeAmount = lateFee;
                order.Status = BorrowStatus.Overdue;
            }

            if ((order.DueDate.Date - now).Days <= 2 || order.Status == BorrowStatus.Overdue)
            {
                var body = $"Đơn #{order.Id} đến hạn trả ngày {order.DueDate:dd/MM/yyyy}. Phí trễ: {order.LateFeeAmount:N0}đ";
                notificationSender.SendEmail(user.Email, "Nhắc nhở trả sách thư viện", body);
                repository.SaveNotification(new Notification
                {
                    UserId = user.Id,
                    Channel = "email+inapp",
                    Message = body,
                    SentAt = DateTime.UtcNow,
                    Kind = order.Status == BorrowStatus.Overdue ? "warning" : "due"
                });
                reminderCount++;
            }
        }

        return reminderCount;
    }
}

public class AchievementService(ILibraryRepository repository)
{
    public List<Achievement> GetUserAchievements(int userId)
    {
        var orders = repository.GetOrders().Where(o => o.UserId == userId).ToList();
        var borrowed = orders.SelectMany(o => o.Items).Where(i => !i.IsPurchase).Sum(i => i.Quantity);
        var purchased = orders.SelectMany(o => o.Items).Where(i => i.IsPurchase).Sum(i => i.Quantity);
        var spent = orders.Sum(o => o.TotalAmount);
        var onTime = orders.Count(o => o.Status == BorrowStatus.Returned || (o.Status == BorrowStatus.Borrowing && o.DueDate >= DateTime.UtcNow));

        return
        [
            Make("BOOKWORM", "🐛", "Bookworm", "Mượn đủ 10 cuốn sách", borrowed, 10),
            Make("COLLECTOR", "📚", "Collector", "Mua 5 cuốn sách về bộ sưu tập", purchased, 5),
            Make("VIP", "💎", "VIP Reader", "Chi tiêu trên 1 triệu đồng", (int)spent, 1_000_000),
            Make("PUNCTUAL", "⚡", "Speed Reader", "5 đơn trả đúng hạn", onTime, 5),
            Make("EXPLORER", "🌍", "Explorer", "Mượn từ 3 danh mục khác nhau",
                orders.SelectMany(o => o.Items).Select(i => repository.GetBook(i.BookId)?.Category).Where(c => c != null).Distinct().Count(), 3),
            Make("REVIEWER", "⭐", "Critic", "Viết 3 đánh giá sách",
                repository.GetBooks().SelectMany(b => b.Reviews).Count(r => r.UserId == userId), 3)
        ];

        static Achievement Make(string code, string icon, string title, string desc, int progress, int target) =>
            new()
            {
                Code = code,
                Icon = icon,
                Title = title,
                Description = desc,
                Progress = Math.Min(progress, target),
                Target = target,
                Unlocked = progress >= target
            };
    }
}

public class RecommendationService(ILibraryRepository repository)
{
    // Collaborative filtering đơn giản:
    // 1. Tìm những user đã mượn trùng sách với user hiện tại
    // 2. Lấy các sách họ mượn mà user hiện tại chưa đọc
    // 3. Sắp xếp theo tần suất xuất hiện
    public List<Book> GetForUser(int userId, int take = 6)
    {
        var orders = repository.GetOrders();
        var books = repository.GetBooks();

        var myBookIds = orders.Where(o => o.UserId == userId)
            .SelectMany(o => o.Items).Select(i => i.BookId).ToHashSet();

        if (myBookIds.Count == 0)
        {
            // Cold start: trả sách bán chạy nhất
            return books.OrderByDescending(b => b.NumberSold).Take(take).ToList();
        }

        var similarUsers = orders.Where(o => o.UserId != userId
                && o.Items.Any(i => myBookIds.Contains(i.BookId)))
            .Select(o => o.UserId).Distinct().ToList();

        var candidates = orders.Where(o => similarUsers.Contains(o.UserId))
            .SelectMany(o => o.Items).Select(i => i.BookId)
            .Where(id => !myBookIds.Contains(id))
            .GroupBy(id => id).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();

        var result = candidates.Select(id => books.FirstOrDefault(b => b.Id == id))
            .Where(b => b != null).Select(b => b!).Take(take).ToList();

        // Nếu thiếu thì fill bằng sách bán chạy
        if (result.Count < take)
        {
            var extras = books.Where(b => !myBookIds.Contains(b.Id) && !result.Contains(b))
                .OrderByDescending(b => b.NumberSold).Take(take - result.Count);
            result.AddRange(extras);
        }
        return result;
    }

    public List<Book> GetSimilarToBook(int bookId, int take = 4)
    {
        var orders = repository.GetOrders();
        var books = repository.GetBooks();
        var source = books.FirstOrDefault(b => b.Id == bookId);
        if (source is null) return [];

        // Những user đã mượn sách này
        var users = orders.Where(o => o.Items.Any(i => i.BookId == bookId))
            .Select(o => o.UserId).Distinct().ToList();

        var candidates = orders.Where(o => users.Contains(o.UserId))
            .SelectMany(o => o.Items).Select(i => i.BookId)
            .Where(id => id != bookId)
            .GroupBy(id => id).OrderByDescending(g => g.Count()).Select(g => g.Key);

        var result = candidates.Select(id => books.FirstOrDefault(b => b.Id == id))
            .Where(b => b != null).Select(b => b!).Take(take).ToList();

        if (result.Count < take)
        {
            var extras = books.Where(b => b.Id != bookId && b.Category == source.Category && !result.Contains(b))
                .Take(take - result.Count);
            result.AddRange(extras);
        }
        return result;
    }
}

public class AnalyticsService(ILibraryRepository repository)
{
    public object GetDashboardStats()
    {
        var orders = repository.GetOrders();
        var books = repository.GetBooks();

        // Doanh thu 6 tháng gần nhất
        var now = DateTime.UtcNow;
        var monthsRev = Enumerable.Range(0, 6).Reverse().Select(i =>
        {
            var m = now.AddMonths(-i);
            var label = $"{m.Month}/{m.Year}";
            var revenue = orders.Where(o => o.BorrowDate.Year == m.Year && o.BorrowDate.Month == m.Month)
                .Sum(o => o.TotalAmount);
            return new { label, revenue };
        }).ToList();

        // Top 5 sách bán chạy / mượn nhiều
        var topBooks = books.OrderByDescending(b => b.NumberSold).Take(5)
            .Select(b => new { name = b.Name, sold = b.NumberSold, borrow = b.BorrowCount }).ToList();

        // Tỉ lệ mượn vs mua
        var items = orders.SelectMany(o => o.Items).ToList();
        var borrowCount = items.Where(i => !i.IsPurchase).Sum(i => i.Quantity);
        var buyCount = items.Where(i => i.IsPurchase).Sum(i => i.Quantity);

        // Phân bố danh mục
        var byCategory = books.GroupBy(b => string.IsNullOrEmpty(b.Category) ? "Khác" : b.Category)
            .Select(g => new { category = g.Key, count = g.Count(), sold = g.Sum(b => b.NumberSold) })
            .OrderByDescending(x => x.sold).ToList();

        // Trạng thái đơn
        var statusDist = orders.GroupBy(o => o.Status.ToString())
            .Select(g => new { status = g.Key, count = g.Count() }).ToList();

        return new { monthsRev, topBooks, borrowCount, buyCount, byCategory, statusDist };
    }
}
