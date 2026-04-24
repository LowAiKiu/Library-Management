namespace LibraryManagement.Domain;

public enum UserRole
{
    Admin = 1,
    Librarian = 2,
    User = 3
}

public enum BorrowStatus
{
    Pending = 1,
    Borrowing = 2,
    Returned = 3,
    Overdue = 4,
    Purchased = 5
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3
}

public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsGoogleLinked { get; set; }
}

public class Book
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CoverImageUrl { get; set; } = string.Empty;
    public int InventoryCount { get; set; }
    public int BorrowCount { get; set; }
    public decimal BasePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public int NumberSold { get; set; }
    public int NumberPage { get; set; }
    public string DatePublication { get; set; } = string.Empty;
    public List<BookReview> Reviews { get; set; } = [];
}

public class BookReview
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BorrowOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public BorrowStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public List<BorrowOrderItem> Items { get; set; } = [];
}

public class BorrowOrderItem
{
    public int Id { get; set; }
    public int BorrowOrderId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsPurchase { get; set; }
}

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    public bool IsActive { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BorrowOrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public PaymentStatus Status { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public string Kind { get; set; } = "info"; // info, warning, success, due
}

public class Achievement
{
    public string Code { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Unlocked { get; set; }
    public int Progress { get; set; }
    public int Target { get; set; }
}
