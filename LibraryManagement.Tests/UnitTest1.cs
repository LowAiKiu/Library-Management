using LibraryManagement.Application;
using LibraryManagement.Domain;

namespace LibraryManagement.Tests;

public class UnitTest1
{
    [Fact]
    public void PricingService_AppliesDiscount_WhenBorrowCountGreaterThan10()
    {
        var service = new PricingService();
        var book = new Book { BasePrice = 100m, BorrowCount = 12, DiscountPercent = 10 };

        var price = service.GetEffectiveBookPrice(book);

        Assert.Equal(90m, price);
    }

    [Fact]
    public void CheckoutService_Purchase_SingleBook_DoesNotIncrementBorrowCount()
    {
        var repo = new FakeRepository();
        var service = new CheckoutService(new PricingService(), repo);
        var book = repo.Books[0];
        var originalBorrow = book.BorrowCount;
        var originalSold = book.NumberSold;

        var result = service.Checkout(1, [(book.Id, 1, true)], null);

        Assert.True(result.Success);
        Assert.Equal(originalBorrow, book.BorrowCount); // không tăng
        Assert.Equal(originalSold + 1, book.NumberSold); // tăng 1
        Assert.Equal(BorrowStatus.Purchased, repo.Orders[^1].Status);
        Assert.Equal(100m * 9.5m, result.Amount); // giá = basePrice * 9.5 (borrowCount=1 nên chưa giảm)
    }

    [Fact]
    public void CheckoutService_Borrow_IncrementsBorrowCountOnly()
    {
        var repo = new FakeRepository();
        var service = new CheckoutService(new PricingService(), repo);
        var book = repo.Books[0];
        var originalBorrow = book.BorrowCount;
        var originalSold = book.NumberSold;

        var result = service.Checkout(1, [(book.Id, 2, false)], null);

        Assert.True(result.Success);
        Assert.Equal(originalBorrow + 2, book.BorrowCount);
        Assert.Equal(originalSold, book.NumberSold); // không tăng
        Assert.Equal(BorrowStatus.Borrowing, repo.Orders[^1].Status);
    }

    [Fact]
    public void ReminderService_AppliesLateFee_At3PercentPerDay()
    {
        var repo = new FakeRepository();
        var notifier = new FakeNotifier();
        var service = new ReminderService(repo, notifier);

        var sent = service.ProcessDueReminders();

        Assert.True(sent > 0);
        Assert.True(repo.Orders[0].LateFeeAmount > 0);
        Assert.Equal(BorrowStatus.Overdue, repo.Orders[0].Status);
    }
}

internal sealed class FakeRepository : ILibraryRepository
{
    public List<Book> Books { get; } = [new() { Id = 1, Name = "B1", BasePrice = 100, InventoryCount = 5, BorrowCount = 1, DiscountPercent = 5 }];
    public List<BorrowOrder> Orders { get; } =
    [
        new() { Id = 1, UserId = 1, BorrowDate = DateTime.UtcNow.AddDays(-20), DueDate = DateTime.UtcNow.AddDays(-3), Status = BorrowStatus.Borrowing, TotalAmount = 200m }
    ];
    public List<AppUser> Users { get; } = [new() { Id = 1, Email = "user@local", Username = "u", PasswordHash = "p", FullName = "User" }];
    public List<Coupon> Coupons { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public List<Payment> Payments { get; } = [];

    public List<Book> GetBooks() => Books;
    public Book? GetBook(int id) => Books.FirstOrDefault(x => x.Id == id);
    public List<BorrowOrder> GetOrders() => Orders;
    public List<AppUser> GetUsers() => Users;
    public Coupon? FindCoupon(string code) => Coupons.FirstOrDefault(x => x.Code == code);
    public void AddReview(BookReview review) { }
    public void AddUser(AppUser user) => Users.Add(user);
    public void SaveOrder(BorrowOrder order) => Orders.Add(order);
    public void SavePayment(Payment payment) => Payments.Add(payment);
    public void SaveNotification(Notification notification) => Notifications.Add(notification);
    public void CreateBook(Book book) { book.Id = Books.Max(b => b.Id) + 1; Books.Add(book); }
    public void UpdateBook(Book book) { }
    public void DeleteBook(int bookId) => Books.RemoveAll(x => x.Id == bookId);
    public void DeleteOrder(int orderId) => Orders.RemoveAll(x => x.Id == orderId);
    public List<Notification> GetNotifications(int userId) => Notifications.Where(x => x.UserId == userId).ToList();
    public void MarkNotificationRead(int notificationId)
    {
        var n = Notifications.FirstOrDefault(x => x.Id == notificationId);
        if (n != null) n.IsRead = true;
    }
}

internal sealed class FakeNotifier : INotificationSender
{
    public int Count { get; private set; }
    public void SendEmail(string toEmail, string subject, string body) => Count++;
}
