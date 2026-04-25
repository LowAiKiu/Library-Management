using LibraryManagement.Application;
using LibraryManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Infrastructure;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookReview> Reviews => Set<BookReview>();
    public DbSet<BorrowOrder> BorrowOrders => Set<BorrowOrder>();
    public DbSet<BorrowOrderItem> BorrowOrderItems => Set<BorrowOrderItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
}

public class EfLibraryRepository(LibraryDbContext dbContext) : ILibraryRepository
{
    public List<Book> GetBooks() => dbContext.Books.Include(x => x.Reviews).ToList();
    public Book? GetBook(int id) => dbContext.Books.Include(x => x.Reviews).FirstOrDefault(x => x.Id == id);
    public List<BorrowOrder> GetOrders() => dbContext.BorrowOrders.Include(x => x.Items).ToList();
    public List<AppUser> GetUsers() => dbContext.Users.ToList();
    public Coupon? FindCoupon(string code) => dbContext.Coupons.FirstOrDefault(x => x.Code == code && x.IsActive);

    public void AddReview(BookReview review)
    {
        dbContext.Reviews.Add(review);
        dbContext.SaveChanges();
    }

    public void AddUser(AppUser user)
    {
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
    }

    public void SaveOrder(BorrowOrder order)
    {
        dbContext.BorrowOrders.Add(order);
        dbContext.SaveChanges();
    }

    public void SavePayment(Payment payment)
    {
        dbContext.Payments.Add(payment);
        dbContext.SaveChanges();
    }

    public void SaveNotification(Notification notification)
    {
        dbContext.Notifications.Add(notification);
        dbContext.SaveChanges();
    }

    public void CreateBook(Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Name))
            throw new InvalidOperationException("Tên sách không được trống.");
        if (book.InventoryCount < 0) book.InventoryCount = 0;
        if (book.BasePrice < 0) book.BasePrice = 0;
        dbContext.Books.Add(book);
        dbContext.SaveChanges();
    }

    public void UpdateBook(Book book)
    {
        if (book.InventoryCount < 0) book.InventoryCount = 0;
        if (book.BasePrice < 0) book.BasePrice = 0;
        // Nếu entity đã được track (cùng Id) thì chỉ SaveChanges; nếu detached thì attach + Modified
        var tracked = dbContext.ChangeTracker.Entries<Book>().FirstOrDefault(e => e.Entity.Id == book.Id)?.Entity;
        if (tracked is null)
        {
            dbContext.Books.Update(book);
        }
        dbContext.SaveChanges();
    }

    public void DeleteBook(int bookId)
    {
        var book = dbContext.Books.Include(b => b.Reviews).FirstOrDefault(x => x.Id == bookId);
        if (book is null) return;
        // Nếu còn order item tham chiếu sách thì không cho xóa (bảo toàn lịch sử đơn)
        var hasOrderItems = dbContext.BorrowOrderItems.Any(i => i.BookId == bookId);
        if (hasOrderItems)
        {
            throw new InvalidOperationException("Không thể xoá sách đã từng có trong đơn hàng.");
        }
        dbContext.Reviews.RemoveRange(book.Reviews);
        dbContext.Books.Remove(book);
        dbContext.SaveChanges();
    }

    public void DeleteOrder(int orderId)
    {
        var order = dbContext.BorrowOrders.Include(x => x.Items).FirstOrDefault(x => x.Id == orderId);
        if (order is null)
        {
            return;
        }
        dbContext.BorrowOrderItems.RemoveRange(order.Items);
        dbContext.BorrowOrders.Remove(order);
        dbContext.SaveChanges();
    }

    public List<Notification> GetNotifications(int userId) =>
        dbContext.Notifications.Where(x => x.UserId == userId).OrderByDescending(x => x.SentAt).Take(30).ToList();

    public void MarkNotificationRead(int notificationId)
    {
        var n = dbContext.Notifications.FirstOrDefault(x => x.Id == notificationId);
        if (n is null) return;
        n.IsRead = true;
        dbContext.SaveChanges();
    }
}

public class ConsoleNotificationSender : INotificationSender
{
    public void SendEmail(string toEmail, string subject, string body)
    {
        // WPF không có console gắn, dùng Debug để tránh IOException khi stdout bị redirect
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SMTP MOCK] To={toEmail} Subject={subject} Body={body}");
        }
        catch
        {
            // không bao giờ phá luồng business chỉ vì log
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("LibraryDb")
            ?? "Server=localhost;Database=LibraryManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<LibraryDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<ILibraryRepository, EfLibraryRepository>();
        services.AddScoped<INotificationSender, ConsoleNotificationSender>();
        services.AddScoped<PricingService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<AchievementService>();
        services.AddScoped<RecommendationService>();
        services.AddScoped<AnalyticsService>();
        return services;
    }
}

public static class SeedData
{
    public static void Initialize(LibraryDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (!IsSchemaUpToDate(dbContext))
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        if (dbContext.Users.Any())
        {
            return;
        }

        var admin = new AppUser { FullName = "Admin User", Email = "admin@library.local", Username = "admin", PasswordHash = "admin123", Role = UserRole.Admin };
        var librarian = new AppUser { FullName = "Librarian User", Email = "librarian@library.local", Username = "librarian", PasswordHash = "lib123", Role = UserRole.Librarian };
        var normalUser = new AppUser { FullName = "Normal User", Email = "user@gmail.com", Username = "user", PasswordHash = "user123", Role = UserRole.User, IsGoogleLinked = true, Address = "123 Le Loi, HCMC", Phone = "0909123456" };
        dbContext.Users.AddRange(admin, librarian, normalUser);
        dbContext.SaveChanges();

        var books = new List<Book>
        {
            new() { Name = "De Men Phieu Luu Ky", Author = "To Hoai", Category = "Van hoc", Summary = "Hanh trinh phieu luu cua chu de men dung cam.", CoverImageUrl = "images/books/De_men_To_Hoai.jpg", InventoryCount = 15, BorrowCount = 14, BasePrice = 8500, DiscountPercent = 10, NumberSold = 40, NumberPage = 180, DatePublication = "2010-05-20" },
            new() { Name = "Tu Ay", Author = "To Huu", Category = "Tho", Summary = "Tap tho truyen cam hung.", CoverImageUrl = "images/books/Tu_ay_To_Huu.jpg", InventoryCount = 10, BorrowCount = 5, BasePrice = 7000, DiscountPercent = 5, NumberSold = 22, NumberPage = 200, DatePublication = "2011-08-15" },
            new() { Name = "Luoc Su Kinh Te Hoc", Author = "Niall Kishtainy", Category = "Kinh te", Summary = "Lich su tom tat nganh kinh te.", CoverImageUrl = "images/books/luoc-su-kinh-te-hoc.jpg", InventoryCount = 9, BorrowCount = 12, BasePrice = 15000, DiscountPercent = 10, NumberSold = 30, NumberPage = 320, DatePublication = "2019-02-10" },
            new() { Name = "Ta Ba Lo Tren Dat A", Author = "Rosie Nguyen", Category = "Du ky", Summary = "Hanh trinh kham pha chau A.", CoverImageUrl = "images/books/ta_ba_lo_tren_dat_a.jpg", InventoryCount = 8, BorrowCount = 4, BasePrice = 12000, DiscountPercent = 5, NumberSold = 18, NumberPage = 280, DatePublication = "2015-10-01" },
            new() { Name = "Clean Code", Author = "Robert C. Martin", Category = "IT", Summary = "Guide for writing maintainable code.", CoverImageUrl = "images/books/img-01.jpg", InventoryCount = 12, BorrowCount = 15, BasePrice = 22000, DiscountPercent = 10, NumberSold = 45, NumberPage = 464, DatePublication = "2008-08-01" },
            new() { Name = "Design Patterns", Author = "GoF", Category = "IT", Summary = "Classic software design pattern catalog.", CoverImageUrl = "images/books/img-02.jpg", InventoryCount = 9, BorrowCount = 9, BasePrice = 24000, DiscountPercent = 5, NumberSold = 20, NumberPage = 395, DatePublication = "1994-10-01" },
            new() { Name = "The Pragmatic Programmer", Author = "Andy Hunt", Category = "IT", Summary = "Timeless software practices.", CoverImageUrl = "images/books/img-03.jpg", InventoryCount = 7, BorrowCount = 11, BasePrice = 21000, DiscountPercent = 10, NumberSold = 16, NumberPage = 352, DatePublication = "2019-09-13" },
            new() { Name = "Refactoring", Author = "Martin Fowler", Category = "IT", Summary = "Improve the design of existing code.", CoverImageUrl = "images/books/img-04.jpg", InventoryCount = 6, BorrowCount = 3, BasePrice = 26000, DiscountPercent = 0, NumberSold = 8, NumberPage = 448, DatePublication = "2018-11-19" },
            new() { Name = "Domain-Driven Design", Author = "Eric Evans", Category = "IT", Summary = "Tackling complexity in the heart of software.", CoverImageUrl = "images/books/img-05.jpg", InventoryCount = 5, BorrowCount = 6, BasePrice = 30000, DiscountPercent = 5, NumberSold = 12, NumberPage = 560, DatePublication = "2003-08-01" },
            new() { Name = "You Don't Know JS", Author = "Kyle Simpson", Category = "IT", Summary = "Deep dive into JavaScript.", CoverImageUrl = "images/books/img-06.jpg", InventoryCount = 14, BorrowCount = 13, BasePrice = 18000, DiscountPercent = 10, NumberSold = 25, NumberPage = 278, DatePublication = "2015-12-27" },
            new() { Name = "CLR via C#", Author = "Jeffrey Richter", Category = "IT", Summary = "Microsoft .NET Framework programming.", CoverImageUrl = "images/books/img-07.jpg", InventoryCount = 11, BorrowCount = 7, BasePrice = 32000, DiscountPercent = 0, NumberSold = 14, NumberPage = 896, DatePublication = "2012-11-15" },
            new() { Name = "Code Complete", Author = "Steve McConnell", Category = "IT", Summary = "A practical handbook of software construction.", CoverImageUrl = "images/books/img-08.jpg", InventoryCount = 13, BorrowCount = 8, BasePrice = 25000, DiscountPercent = 5, NumberSold = 17, NumberPage = 960, DatePublication = "2004-06-01" },
            new() { Name = "Tam Ly Hoc Ve Tien", Author = "Morgan Housel", Category = "Kinh te", Summary = "Nhung bai hoc ve tai chinh ca nhan.", CoverImageUrl = "images/books/img-09.jpg", InventoryCount = 20, BorrowCount = 18, BasePrice = 12900, DiscountPercent = 10, NumberSold = 55, NumberPage = 248, DatePublication = "2020-09-08" },
            new() { Name = "Lich Su Thoi Gian", Author = "Stephen Hawking", Category = "Khoa hoc", Summary = "Gioi thieu vu tru hoc.", CoverImageUrl = "images/books/img-10.jpg", InventoryCount = 9, BorrowCount = 12, BasePrice = 16000, DiscountPercent = 5, NumberSold = 28, NumberPage = 256, DatePublication = "1988-04-01" },
            new() { Name = "Nghe Thuat Ql Tai Chinh Ca Nhan", Author = "Napoleon Hill", Category = "Kinh te", Summary = "Huong dan quan ly tai chinh.", CoverImageUrl = "images/books/img-11.jpg", InventoryCount = 16, BorrowCount = 10, BasePrice = 11000, DiscountPercent = 5, NumberSold = 30, NumberPage = 300, DatePublication = "2012-01-01" },
            new() { Name = "Thien Tai Ben Trai", Author = "Cao Minh", Category = "Tam ly", Summary = "Cau chuyen ve nhung nguoi dac biet.", CoverImageUrl = "images/books/img-12.jpg", InventoryCount = 18, BorrowCount = 22, BasePrice = 15000, DiscountPercent = 10, NumberSold = 60, NumberPage = 420, DatePublication = "2015-03-10" }
        };

        foreach (var b in books)
        {
            b.Reviews.Add(new BookReview { UserId = normalUser.Id, ReviewerName = normalUser.FullName, Rating = 5, Content = "Sach rat hay va bo ich.", CreatedAt = DateTime.UtcNow });
        }
        dbContext.Books.AddRange(books);

        dbContext.Coupons.AddRange(
            new Coupon { Code = "SAVE10", Percent = 10, IsActive = true },
            new Coupon { Code = "WELCOME5", Percent = 5, IsActive = true });

        dbContext.SaveChanges();

        // Tạo thêm vài user để analytics/recommendation có dữ liệu thật
        var extraUser1 = new AppUser { FullName = "Nguyen Van A", Email = "a@gmail.com", Username = "usera", PasswordHash = "user123", Role = UserRole.User, Phone = "0903111222", Address = "Hanoi" };
        var extraUser2 = new AppUser { FullName = "Tran Thi B", Email = "b@gmail.com", Username = "userb", PasswordHash = "user123", Role = UserRole.User, Phone = "0903333444", Address = "Da Nang" };
        dbContext.Users.AddRange(extraUser1, extraUser2);
        dbContext.SaveChanges();

        var now = DateTime.UtcNow;
        var savedBooks = dbContext.Books.ToList();
        Book Pick(string name) => savedBooks.First(b => b.Name == name);

        var orders = new List<BorrowOrder>
        {
            new() { UserId = normalUser.Id, BorrowDate = now.AddDays(-10), DueDate = now.AddDays(1), Status = BorrowStatus.Borrowing, TotalAmount = 18000,
                Items = { new BorrowOrderItem { BookId = Pick("Clean Code").Id, Quantity = 1, UnitPrice = 19800, IsPurchase = false } } },
            new() { UserId = normalUser.Id, BorrowDate = now.AddMonths(-1), DueDate = now.AddMonths(-1).AddDays(14), Status = BorrowStatus.Returned, TotalAmount = 15000,
                Items = { new BorrowOrderItem { BookId = Pick("Tam Ly Hoc Ve Tien").Id, Quantity = 1, UnitPrice = 11610, IsPurchase = false } } },
            new() { UserId = normalUser.Id, BorrowDate = now.AddMonths(-2), DueDate = now.AddMonths(-2).AddDays(14), Status = BorrowStatus.Purchased, TotalAmount = 188100,
                Items = { new BorrowOrderItem { BookId = Pick("Design Patterns").Id, Quantity = 1, UnitPrice = 188100, IsPurchase = true } } },
            new() { UserId = extraUser1.Id, BorrowDate = now.AddMonths(-1), DueDate = now.AddMonths(-1).AddDays(14), Status = BorrowStatus.Returned, TotalAmount = 32000,
                Items = { new BorrowOrderItem { BookId = Pick("Clean Code").Id, Quantity = 1, UnitPrice = 19800, IsPurchase = false }, new BorrowOrderItem { BookId = Pick("Design Patterns").Id, Quantity = 1, UnitPrice = 22800, IsPurchase = false } } },
            new() { UserId = extraUser1.Id, BorrowDate = now.AddMonths(-3), DueDate = now.AddMonths(-3).AddDays(14), Status = BorrowStatus.Returned, TotalAmount = 18900,
                Items = { new BorrowOrderItem { BookId = Pick("Refactoring").Id, Quantity = 1, UnitPrice = 26000, IsPurchase = false } } },
            new() { UserId = extraUser2.Id, BorrowDate = now.AddMonths(-2), DueDate = now.AddMonths(-2).AddDays(14), Status = BorrowStatus.Returned, TotalAmount = 12900,
                Items = { new BorrowOrderItem { BookId = Pick("Tam Ly Hoc Ve Tien").Id, Quantity = 1, UnitPrice = 11610, IsPurchase = false } } },
            new() { UserId = extraUser2.Id, BorrowDate = now.AddDays(-20), DueDate = now.AddDays(-6), Status = BorrowStatus.Overdue, TotalAmount = 15000, LateFeeAmount = 2700,
                Items = { new BorrowOrderItem { BookId = Pick("Thien Tai Ben Trai").Id, Quantity = 1, UnitPrice = 13500, IsPurchase = false } } },
            new() { UserId = extraUser1.Id, BorrowDate = now.AddDays(-3), DueDate = now.AddDays(11), Status = BorrowStatus.Borrowing, TotalAmount = 17200,
                Items = { new BorrowOrderItem { BookId = Pick("You Don't Know JS").Id, Quantity = 1, UnitPrice = 16200, IsPurchase = false } } }
        };
        dbContext.BorrowOrders.AddRange(orders);

        dbContext.Notifications.AddRange(
            new Notification { UserId = normalUser.Id, Channel = "inapp", Kind = "due", Message = "Đơn #1 sắp đến hạn trả (còn 1 ngày).", SentAt = now.AddHours(-2), IsRead = false },
            new Notification { UserId = normalUser.Id, Channel = "inapp", Kind = "success", Message = "🎉 Chào mừng bạn đến với Library Management!", SentAt = now.AddDays(-1), IsRead = false },
            new Notification { UserId = normalUser.Id, Channel = "inapp", Kind = "info", Message = "Có 3 sách mới vừa được thêm vào thư viện.", SentAt = now.AddDays(-3), IsRead = true });

        dbContext.SaveChanges();
    }

    private static bool IsSchemaUpToDate(LibraryDbContext dbContext)
    {
        try
        {
            dbContext.Books.Select(b => new { b.Category, b.DatePublication, b.NumberPage, b.NumberSold }).FirstOrDefault();
            dbContext.Reviews.Select(r => new { r.CreatedAt }).FirstOrDefault();
            dbContext.Users.Select(u => new { u.Address, u.Phone }).FirstOrDefault();
            dbContext.BorrowOrderItems.Select(i => new { i.IsPurchase }).FirstOrDefault();
            dbContext.Notifications.Select(n => new { n.IsRead, n.Kind }).FirstOrDefault();

            // Sentinel: nếu seed cũ có giá > 50.000 thì rebuild với giá /10 mới
            var maxSeedPrice = dbContext.Books.OrderByDescending(b => b.BasePrice).Select(b => (decimal?)b.BasePrice).FirstOrDefault();
            if (maxSeedPrice.HasValue && maxSeedPrice.Value >= 50000m)
            {
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
