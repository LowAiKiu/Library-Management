# Library Management — WPF + WebView2 + SQL Server

Ứng dụng quản lý thư viện desktop viết bằng **WPF (.NET 10)**, nhúng UI HTML/CSS/JS qua **WebView2**, dữ liệu lưu trong **SQL Server** với **Entity Framework Core**.

---

## Kiến trúc

| Layer | Trách nhiệm |
|-------|-------------|
| `LibraryManagement.Domain` | Entity: `AppUser`, `Book`, `BorrowOrder`, `Notification`, `Achievement`, enum… |
| `LibraryManagement.Application` | Interface repo, service: `PricingService`, `CheckoutService`, `ReminderService`, `AchievementService`, `RecommendationService`, `AnalyticsService` |
| `LibraryManagement.Infrastructure` | `LibraryDbContext`, `EfLibraryRepository`, `ConsoleNotificationSender`, `SeedData`, DI |
| `LibraryManagement.Wpf` | `MainWindow` hosts WebView2, JS SPA renderer, PDF exporter, event bridge |
| `LibraryManagement.Tests` | xUnit: PricingService, ReminderService |

## Tính năng chính

### Cơ bản
- **3 vai trò**: Admin / Librarian / User (role-based menu)
- **Login** nội bộ + Google mock
- **Giỏ hàng** với radio **Mượn / Mua** (giá mua = 9.5 × giá mượn)
- **Giảm 5-10%** cho sách mượn > 10 lần
- **Phí trễ 3%/ngày** tự tính khi quá hạn
- **Thanh toán** mock, coupon `SAVE10` / `WELCOME5`
- **Review sách** có rating 1-5 sao

### 6 tính năng "wow"
1. **📊 Live Analytics Dashboard** (Admin/Librarian) — 4 biểu đồ Chart.js: doanh thu 6 tháng, top 5 sách bán chạy, mượn vs mua, phân bố danh mục
2. **🔔 Real-time Notification Bell** — chuông trên header, badge số thông báo chưa đọc, dropdown + mark-as-read
3. **🎯 Book Recommendation** — collaborative filtering: gợi ý "Dành riêng cho bạn" ở Home và "Người đọc cuốn này cũng thích" ở Detail
4. **🏆 Gamification** — 6 achievements (Bookworm, Collector, VIP, Speed Reader, Explorer, Critic) + level + progress bar
5. **🌙 Dark Mode** — toggle 1 nút, CSS variables, lưu preference vào `localStorage`
6. **📄 Export PDF** — QuestPDF tạo hóa đơn từng đơn + báo cáo thống kê toàn hệ thống

## Chạy thử

```powershell
# Yêu cầu: .NET 10 SDK, SQL Server (LocalDB/Express), WebView2 runtime
dotnet restore
dotnet run --project LibraryManagement.Wpf
```

Cấu hình DB trong `LibraryManagement.Wpf/appsettings.json`. DB tự drop+recreate khi schema lỗi thời.

### Tài khoản test
| Role | Username | Password |
|------|----------|----------|
| Admin | `admin` | `admin123` |
| Librarian | `librarian` | `lib123` |
| User | `user` | `user123` |
