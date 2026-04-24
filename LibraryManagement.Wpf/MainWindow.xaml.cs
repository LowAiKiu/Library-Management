using LibraryManagement.Application;
using LibraryManagement.Domain;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace LibraryManagement.Wpf;

public partial class MainWindow : Window
{
    private readonly ILibraryRepository _repository;
    private readonly CheckoutService _checkoutService;
    private readonly ReminderService _reminderService;
    private readonly AchievementService _achievementService;
    private readonly RecommendationService _recommendationService;
    private readonly AnalyticsService _analyticsService;
    private readonly Dictionary<int, (int Qty, bool IsPurchase)> _cart = new();

    private AppUser? _currentUser;
    private string _currentView = "login";
    private int? _detailBookId;
    private bool _bridgeReady;

    public MainWindow(
        ILibraryRepository repository,
        CheckoutService checkoutService,
        ReminderService reminderService,
        AchievementService achievementService,
        RecommendationService recommendationService,
        AnalyticsService analyticsService)
    {
        _repository = repository;
        _checkoutService = checkoutService;
        _reminderService = reminderService;
        _achievementService = achievementService;
        _recommendationService = recommendationService;
        _analyticsService = analyticsService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await TemplateWebView.EnsureCoreWebView2Async();
        var templateRoot = Path.Combine(AppContext.BaseDirectory, "WebTemplate");
        TemplateWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "library.app", templateRoot, CoreWebView2HostResourceAccessKind.Allow);
        TemplateWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        TemplateWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

        // Ghi shell.html vào thư mục WebTemplate rồi navigate qua virtual host
        // để cùng origin với CSS/ảnh và window.chrome.webview bridge hoạt động đúng
        var shellPath = Path.Combine(templateRoot, "shell.html");
        File.WriteAllText(shellPath, BuildShellHtml(), System.Text.Encoding.UTF8);
        TemplateWebView.Source = new Uri("https://library.app/shell.html");
    }

    private static string BuildShellHtml() => $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
<meta charset=""UTF-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Library Management</title>
<link rel=""stylesheet"" href=""css/bootstrap.min.css""/>
<link rel=""stylesheet"" href=""css/font-awesome.min.css""/>
<script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
<style>
:root{{
  --bg:#F5F7FA; --card:#FFFFFF; --text:#222; --muted:#888;
  --primary:#1E88E5; --primary-dark:#1565C0;
  --accent:#FFF9C4; --accent-border:#e0c800;
  --border:#e8e8e8; --hover:#F8FBFF; --th-bg:#EEF5FF;
  --login-bg:linear-gradient(135deg,#E3F2FD 0%,#FFF9C4 100%);
  --shadow:0 2px 8px rgba(0,0,0,0.07);
  --shadow-hover:0 6px 18px rgba(0,0,0,0.12);
}}
body.dark{{
  --bg:#0F172A; --card:#1E293B; --text:#E2E8F0; --muted:#94A3B8;
  --primary:#60A5FA; --primary-dark:#3B82F6;
  --accent:#FDE68A; --accent-border:#F59E0B;
  --border:#334155; --hover:#334155; --th-bg:#1E293B;
  --login-bg:linear-gradient(135deg,#1E293B 0%,#0F172A 100%);
  --shadow:0 2px 8px rgba(0,0,0,0.4);
  --shadow-hover:0 6px 18px rgba(0,0,0,0.6);
}}
*{{box-sizing:border-box;margin:0;padding:0;}}
body{{background:var(--bg);font-family:'Segoe UI',Arial,sans-serif;color:var(--text);min-height:100vh;transition:background .3s,color .3s;}}
header{{background:linear-gradient(90deg,var(--primary-dark),var(--primary));color:white;padding:12px 24px;display:flex;justify-content:space-between;align-items:center;box-shadow:0 2px 8px rgba(0,0,0,0.18);position:sticky;top:0;z-index:100;gap:12px;flex-wrap:wrap;}}
.logo{{font-size:20px;font-weight:700;letter-spacing:.5px;display:flex;align-items:center;gap:8px;}}
.nav-links{{display:flex;gap:6px;flex-wrap:wrap;align-items:center;}}
.nav-links a{{display:inline-block;padding:6px 14px;background:rgba(255,255,255,0.15);color:white;border-radius:20px;text-decoration:none;cursor:pointer;font-size:13px;border:1px solid rgba(255,255,255,0.25);transition:background .15s;}}
.nav-links a:hover{{background:rgba(255,255,255,0.3);}}
.nav-links a.active{{background:var(--accent);color:var(--primary-dark);border-color:var(--accent);font-weight:600;}}
.icon-btn{{width:36px;height:36px;border-radius:50%;background:rgba(255,255,255,0.15);border:1px solid rgba(255,255,255,0.25);display:inline-flex;align-items:center;justify-content:center;cursor:pointer;color:white;position:relative;font-size:16px;transition:background .15s;}}
.icon-btn:hover{{background:rgba(255,255,255,0.3);}}
.notif-badge{{position:absolute;top:-4px;right:-4px;background:#EF4444;color:white;border-radius:10px;padding:1px 6px;font-size:10px;font-weight:700;min-width:18px;text-align:center;}}
.notif-dropdown{{position:absolute;top:44px;right:0;background:var(--card);color:var(--text);border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,0.2);width:340px;max-height:420px;overflow:auto;display:none;z-index:200;border:1px solid var(--border);}}
.notif-dropdown.show{{display:block;}}
.notif-head{{padding:12px 16px;border-bottom:1px solid var(--border);font-weight:700;color:var(--primary-dark);font-size:14px;display:flex;justify-content:space-between;align-items:center;}}
.notif-item{{padding:10px 16px;border-bottom:1px solid var(--border);cursor:pointer;transition:background .15s;font-size:13px;}}
.notif-item:hover{{background:var(--hover);}}
.notif-item.unread{{background:rgba(30,136,229,0.06);border-left:3px solid var(--primary);}}
.notif-item .meta{{font-size:11px;color:var(--muted);margin-top:4px;}}
.page{{max-width:1200px;margin:0 auto;padding:24px 16px;}}
.login-wrap{{min-height:calc(100vh - 60px);display:flex;align-items:center;justify-content:center;background:var(--login-bg);}}
.login-card{{background:var(--card);border-radius:12px;box-shadow:0 8px 32px rgba(21,101,192,0.15);padding:40px;width:420px;max-width:95vw;color:var(--text);}}
.login-card h2{{color:var(--primary-dark);margin-bottom:6px;font-size:24px;}}
.login-card p{{color:var(--muted);margin-bottom:24px;font-size:14px;}}
.form-group{{margin-bottom:16px;}}
.form-group label{{display:block;font-size:13px;font-weight:600;color:var(--text);margin-bottom:6px;}}
.form-group input{{width:100%;padding:11px 14px;border:1.5px solid var(--border);border-radius:6px;font-size:14px;transition:border .15s;background:var(--card);color:var(--text);}}
.form-group input:focus{{outline:none;border-color:var(--primary);box-shadow:0 0 0 3px rgba(30,136,229,.1);}}
.btn{{display:inline-flex;align-items:center;justify-content:center;gap:6px;padding:10px 20px;border:none;border-radius:6px;cursor:pointer;font-size:14px;font-weight:500;transition:all .15s;text-decoration:none;}}
.btn-primary{{background:var(--primary);color:white;}}
.btn-primary:hover{{background:var(--primary-dark);}}
.btn-full{{width:100%;}}
.btn-yellow{{background:var(--accent);color:#333;border:1px solid var(--accent-border);}}
.btn-yellow:hover{{background:#fff176;}}
.btn-red{{background:#E53935;color:white;font-size:12px;padding:5px 10px;}}
.btn-red:hover{{background:#C62828;}}
.btn-sm{{padding:5px 12px;font-size:12px;}}
.badge{{display:inline-block;padding:3px 10px;border-radius:12px;font-size:11px;font-weight:700;}}
.badge-blue{{background:#E3F2FD;color:#1565C0;}}
.badge-red{{background:#FFEBEE;color:#C62828;}}
.badge-green{{background:#E8F5E9;color:#2E7D32;}}
.badge-yellow{{background:#FFF9C4;color:#F57F17;border:1px solid #e0c800;}}
.hint-box{{background:var(--accent);border:1px solid var(--accent-border);border-radius:6px;padding:10px 14px;font-size:13px;margin-bottom:18px;color:#555;}}
.card-grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:16px;}}
.book-card{{background:var(--card);border:1px solid var(--border);border-radius:10px;overflow:hidden;box-shadow:var(--shadow);transition:transform .15s,box-shadow .15s;display:flex;flex-direction:column;color:var(--text);}}
.book-card:hover{{transform:translateY(-3px);box-shadow:var(--shadow-hover);}}
.book-img{{width:100%;height:210px;object-fit:cover;background:#EEF5FF;}}
.book-body{{padding:12px;flex:1;display:flex;flex-direction:column;gap:4px;}}
.book-name{{font-weight:700;color:var(--primary-dark);font-size:14px;line-height:1.3;}}
.book-meta{{font-size:12px;color:var(--muted);}}
.price{{font-size:16px;font-weight:700;color:#E53935;}}
.price-old{{text-decoration:line-through;color:#aaa;font-size:13px;}}
.book-actions{{display:flex;gap:6px;margin-top:8px;}}
.book-actions .btn{{flex:1;font-size:12px;padding:7px;}}
.hero{{background:linear-gradient(120deg,var(--primary-dark) 0%,var(--primary) 60%,var(--accent) 100%);color:white;border-radius:12px;padding:32px;margin-bottom:24px;}}
.hero h2{{font-size:26px;margin-bottom:8px;}}
.hero p{{opacity:.9;margin-bottom:16px;}}
table{{width:100%;border-collapse:collapse;background:var(--card);border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.06);color:var(--text);}}
th{{background:var(--th-bg);padding:12px 10px;text-align:left;font-size:13px;font-weight:700;color:var(--primary-dark);border-bottom:2px solid var(--border);}}
td{{padding:10px 10px;border-bottom:1px solid var(--border);font-size:13px;}}
tr:hover td{{background:var(--hover);}}
.stat-cards{{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:14px;margin-bottom:20px;}}
.stat-card{{background:var(--card);border-radius:10px;padding:20px;box-shadow:var(--shadow);border-left:4px solid var(--primary);color:var(--text);}}
.stat-card.yellow{{border-left-color:var(--accent-border);}}
.stat-card.red{{border-left-color:#E53935;}}
.stat-card.green{{border-left-color:#10B981;}}
.stat-number{{font-size:28px;font-weight:700;color:var(--primary-dark);}}
.stat-label{{font-size:13px;color:var(--muted);margin-top:4px;}}
.section-title{{color:var(--primary-dark);font-size:18px;font-weight:700;margin:20px 0 12px;display:flex;align-items:center;gap:8px;}}
.detail-grid{{display:grid;grid-template-columns:280px 1fr;gap:24px;}}
@media(max-width:700px){{.detail-grid{{grid-template-columns:1fr;}}}}
.search-bar{{display:flex;gap:8px;margin-bottom:16px;}}
.search-bar input{{flex:1;}}
input,select,textarea{{padding:9px 12px;border:1.5px solid var(--border);border-radius:6px;font-size:14px;transition:border .15s;background:var(--card);color:var(--text);}}
input:focus,select:focus,textarea:focus{{outline:none;border-color:var(--primary);box-shadow:0 0 0 3px rgba(30,136,229,.1);}}
.cart-table img{{width:52px;height:52px;object-fit:cover;border-radius:6px;vertical-align:middle;margin-right:8px;}}
.review-item{{background:var(--hover);padding:10px 14px;border-radius:8px;margin-bottom:8px;border-left:3px solid var(--primary);}}
.chart-grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:16px;margin-bottom:24px;}}
.chart-card{{background:var(--card);border-radius:10px;padding:16px;box-shadow:var(--shadow);color:var(--text);}}
.chart-card h3{{color:var(--primary-dark);font-size:14px;margin-bottom:10px;}}
.chart-card canvas{{max-height:260px;}}
.achievement-grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:12px;}}
.achv-card{{background:var(--card);border:2px solid var(--border);border-radius:10px;padding:14px;text-align:center;transition:all .2s;color:var(--text);}}
.achv-card.unlocked{{border-color:var(--accent-border);background:linear-gradient(135deg,var(--card) 0%,rgba(255,249,196,0.3) 100%);}}
.achv-card.unlocked .achv-icon{{filter:none;animation:pulse 2s ease-in-out infinite;}}
.achv-icon{{font-size:40px;margin-bottom:6px;filter:grayscale(1) opacity(.4);}}
.achv-title{{font-weight:700;color:var(--primary-dark);font-size:13px;margin-bottom:2px;}}
.achv-desc{{font-size:11px;color:var(--muted);min-height:30px;}}
.progress-bar{{height:6px;background:var(--border);border-radius:3px;overflow:hidden;margin-top:8px;}}
.progress-fill{{height:100%;background:linear-gradient(90deg,var(--primary),var(--accent-border));transition:width .5s;}}
@keyframes pulse{{0%,100%{{transform:scale(1);}}50%{{transform:scale(1.08);}}}}
.recommend-strip{{background:linear-gradient(135deg,rgba(30,136,229,0.05),rgba(255,249,196,0.15));border:1px solid var(--border);border-radius:12px;padding:16px;margin-bottom:20px;}}
.notif-kind-due{{border-left-color:#F59E0B;}}
.notif-kind-warning{{border-left-color:#EF4444;}}
.notif-kind-success{{border-left-color:#10B981;}}
.modal-backdrop{{display:none;position:fixed;inset:0;background:rgba(0,0,0,.55);z-index:300;align-items:flex-start;justify-content:center;padding:40px 16px;overflow-y:auto;}}
.modal-backdrop.show{{display:flex;}}
.modal-card{{background:var(--card);color:var(--text);border-radius:12px;padding:24px;width:640px;max-width:95vw;box-shadow:0 12px 40px rgba(0,0,0,.3);}}
.modal-card .form-group{{margin-bottom:10px;}}
.modal-card .form-group label{{font-size:12px;color:var(--muted);}}
.modal-card textarea{{width:100%;padding:9px 12px;border:1.5px solid var(--border);border-radius:6px;background:var(--card);color:var(--text);font-family:inherit;}}
</style>
</head>
<body>
<div id=""root""></div>
<script>
(function(){{
  window.callHost = function(d){{ window.chrome.webview.postMessage(JSON.stringify(d)); }};
  window.appState = {{ view:'login' }};
{SpaRendererScript}
  document.addEventListener('DOMContentLoaded', function(){{
    try{{ if(localStorage.getItem('dark')==='1') document.body.classList.add('dark'); }}catch(e){{}}
    window.renderApp();
  }});
}})();
</script>
</body>
</html>";


    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // Shell HTML đã nhúng SpaRendererScript rồi — chỉ cần push state là renderApp tự chạy
        _bridgeReady = true;
        await PushStateAndRenderAsync();
    }

    private object BuildAppStateObject()
    {
        var books = _repository.GetBooks();
        var orders = _repository.GetOrders();
        var users = _repository.GetUsers();

        var notifications = _currentUser is null
            ? Array.Empty<object>().AsEnumerable()
            : _repository.GetNotifications(_currentUser.Id).Select(n => new { n.Id, n.Message, n.SentAt, n.IsRead, n.Kind });

        var achievements = _currentUser is null
            ? Array.Empty<object>().AsEnumerable()
            : _achievementService.GetUserAchievements(_currentUser.Id)
                .Select(a => new { a.Code, a.Icon, a.Title, a.Description, a.Unlocked, a.Progress, a.Target });

        var recommendations = _currentUser is null
            ? Array.Empty<object>().AsEnumerable()
            : _recommendationService.GetForUser(_currentUser.Id).Select(b => new
            {
                b.Id, b.Name, b.Author, b.Category, b.CoverImageUrl,
                b.InventoryCount, b.BorrowCount, b.BasePrice, b.DiscountPercent,
                b.NumberSold, b.NumberPage, b.DatePublication, b.Summary,
                Reviews = b.Reviews.Select(r => new { r.ReviewerName, r.Rating, r.Content, r.CreatedAt })
            });

        var similar = _detailBookId is null
            ? Array.Empty<object>().AsEnumerable()
            : _recommendationService.GetSimilarToBook(_detailBookId.Value).Select(b => new
            {
                b.Id, b.Name, b.Author, b.Category, b.CoverImageUrl,
                b.InventoryCount, b.BorrowCount, b.BasePrice, b.DiscountPercent,
                b.NumberSold, b.NumberPage, b.DatePublication, b.Summary,
                Reviews = b.Reviews.Select(r => new { r.ReviewerName, r.Rating, r.Content, r.CreatedAt })
            });

        var analytics = _analyticsService.GetDashboardStats();

        return new
        {
            view = _currentView,
            detailBookId = _detailBookId,
            role = _currentUser?.Role.ToString(),
            user = _currentUser is null ? null : new { _currentUser.Id, _currentUser.FullName, _currentUser.Email, _currentUser.Username, _currentUser.Address, _currentUser.Phone, Role = _currentUser.Role.ToString() },
            books = books.Select(x => new
            {
                x.Id,
                x.Name,
                x.Author,
                x.Category,
                x.Summary,
                x.CoverImageUrl,
                x.InventoryCount,
                x.BorrowCount,
                x.BasePrice,
                x.DiscountPercent,
                x.NumberSold,
                x.NumberPage,
                x.DatePublication,
                Reviews = x.Reviews.Select(r => new { r.ReviewerName, r.Rating, r.Content, r.CreatedAt })
            }),
            orders = orders.Select(x => new
            {
                x.Id,
                x.UserId,
                x.BorrowDate,
                x.DueDate,
                Status = x.Status.ToString(),
                x.TotalAmount,
                x.LateFeeAmount,
                UserEmail = users.FirstOrDefault(u => u.Id == x.UserId)?.Email,
                UserName = users.FirstOrDefault(u => u.Id == x.UserId)?.FullName,
                Items = x.Items.Select(i => new { i.BookId, i.Quantity, i.UnitPrice, i.IsPurchase, BookName = books.FirstOrDefault(b => b.Id == i.BookId)?.Name })
            }),
            users = users.Select(x => new { x.Id, x.FullName, x.Email, x.Username, x.Phone, x.Address, Role = x.Role.ToString() }),
            cart = _cart.Select(kv =>
            {
                var b = books.FirstOrDefault(x => x.Id == kv.Key);
                var borrowPrice = b is null ? 0 : Math.Round(b.BorrowCount > 10 ? b.BasePrice * (1 - Math.Clamp(b.DiscountPercent, 5, 10) / 100m) : b.BasePrice, 0);
                var buyPrice = Math.Round(borrowPrice * CheckoutService.BuyMultiplier, 0);
                return new
                {
                    bookId = kv.Key,
                    quantity = kv.Value.Qty,
                    isPurchase = kv.Value.IsPurchase,
                    name = b?.Name,
                    cover = b?.CoverImageUrl,
                    borrowPrice,
                    buyPrice,
                    effectivePrice = kv.Value.IsPurchase ? buyPrice : borrowPrice
                };
            }),
            notifications,
            achievements,
            recommendations,
            similar,
            analytics
        };
    }

    private async Task PushStateAndRenderAsync()
    {
        if (!_bridgeReady)
        {
            return;
        }

        var state = BuildAppStateObject();
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var script = $"window.appState = {json}; window.renderApp();";
        await TemplateWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";

        switch (action)
        {
            case "login":
                HandleLogin(root);
                break;
            case "signup":
                HandleSignup(root);
                break;
            case "googleLogin":
                HandleGoogleLogin();
                break;
            case "logout":
                _currentUser = null;
                _cart.Clear();
                _currentView = "login";
                break;
            case "nav":
                var target = root.TryGetProperty("target", out var t) ? t.GetString() ?? "home" : "home";
                _currentView = RequiresLogin(target) && _currentUser is null ? "login" : target;
                break;
            case "detail":
                _detailBookId = root.TryGetProperty("bookId", out var bId) ? bId.GetInt32() : null;
                _currentView = "detail";
                break;
            case "addToCart":
                AddToCart(root);
                break;
            case "removeCart":
                if (root.TryGetProperty("bookId", out var rid))
                {
                    _cart.Remove(rid.GetInt32());
                }
                break;
            case "updateQty":
                if (root.TryGetProperty("bookId", out var uid) && root.TryGetProperty("quantity", out var qty))
                {
                    var q = qty.GetInt32();
                    if (q <= 0) _cart.Remove(uid.GetInt32());
                    else if (_cart.TryGetValue(uid.GetInt32(), out var existing))
                        _cart[uid.GetInt32()] = (q, existing.IsPurchase);
                }
                break;
            case "checkout":
                HandleCheckout(root);
                break;
            case "submitReview":
                HandleSubmitReview(root);
                break;
            case "deleteOrder":
                if (root.TryGetProperty("orderId", out var oid))
                {
                    _repository.DeleteOrder(oid.GetInt32());
                }
                break;
            case "reminder":
                var sent = _reminderService.ProcessDueReminders();
                MessageBox.Show($"Đã gửi {sent} thông báo nhắc trả sách.");
                break;
            case "markNotifRead":
                if (root.TryGetProperty("notifId", out var nid))
                {
                    _repository.MarkNotificationRead(nid.GetInt32());
                }
                break;
            case "markAllNotifRead":
                if (_currentUser != null)
                {
                    foreach (var n in _repository.GetNotifications(_currentUser.Id).Where(x => !x.IsRead))
                    {
                        _repository.MarkNotificationRead(n.Id);
                    }
                }
                break;
            case "exportInvoicePdf":
                if (root.TryGetProperty("orderId", out var eoid))
                {
                    ExportInvoicePdf(eoid.GetInt32());
                }
                break;
            case "exportAnalyticsPdf":
                ExportAnalyticsPdf();
                break;
            case "createBook":
                HandleCreateBook(root);
                break;
            case "updateBook":
                HandleUpdateBook(root);
                break;
            case "deleteBook":
                HandleDeleteBook(root);
                break;
        }

        _ = PushStateAndRenderAsync();
    }

    private static bool RequiresLogin(string view) =>
        view is "cart" or "orders" or "admin" or "librarian" or "profile" or "checkout" or "analytics" or "notifications" or "achievements";

    private void HandleLogin(JsonElement root)
    {
        var username = root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
        var password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
        var user = _repository.GetUsers().FirstOrDefault(x =>
            (x.Username == username || x.Email == username) && x.PasswordHash == password);
        if (user is null)
        {
            MessageBox.Show("Login failed. Try admin/admin123, librarian/lib123, user/user123.");
            _currentView = "login";
            return;
        }

        _currentUser = user;
        _currentView = user.Role switch
        {
            UserRole.Admin => "admin",
            UserRole.Librarian => "librarian",
            _ => "home"
        };
    }

    private void HandleGoogleLogin()
    {
        var user = _repository.GetUsers().FirstOrDefault(x => x.IsGoogleLinked);
        if (user is null)
        {
            MessageBox.Show("No Google-linked account.");
            return;
        }
        _currentUser = user;
        _currentView = "home";
    }

    private void HandleSignup(JsonElement root)
    {
        string Get(string key) => root.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
        var username = Get("username");
        var password = Get("password");
        var email = Get("email");
        var fullname = Get("fullname");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Missing username or password.");
            return;
        }

        if (_repository.GetUsers().Any(x => x.Username == username))
        {
            MessageBox.Show("Username already exists.");
            return;
        }

        _repository.AddUser(new AppUser
        {
            Username = username,
            PasswordHash = password,
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullname) ? username : fullname,
            Address = Get("address"),
            Phone = Get("phone"),
            Role = UserRole.User
        });
        MessageBox.Show("Sign-up successful. Please login.");
        _currentView = "login";
    }

    private void AddToCart(JsonElement root)
    {
        if (_currentUser is null)
        {
            _currentView = "login";
            return;
        }

        if (!root.TryGetProperty("bookId", out var b))
        {
            return;
        }

        var bookId = b.GetInt32();
        var qty = root.TryGetProperty("quantity", out var q) ? Math.Max(1, q.GetInt32()) : 1;
        var isPurchase = root.TryGetProperty("isPurchase", out var ip) && ip.GetBoolean();
        // Thêm số lượng, giữ nguyên isPurchase nếu đã có; nếu chưa có thì dùng isPurchase mới
        var existing = _cart.GetValueOrDefault(bookId);
        _cart[bookId] = (existing.Qty + qty, isPurchase);
    }

    private void HandleCheckout(JsonElement root)
    {
        if (_currentUser is null || _cart.Count == 0)
        {
            MessageBox.Show("Cart is empty or not logged in.");
            return;
        }

        var coupon = root.TryGetProperty("coupon", out var c) ? c.GetString() : null;
        var items = _cart.Select(kv => (kv.Key, kv.Value.Qty, kv.Value.IsPurchase)).ToList();
        var result = _checkoutService.Checkout(_currentUser.Id, items, coupon);
        MessageBox.Show($"{result.Message} Amount: {result.Amount:C} Order #{result.OrderId}");
        if (result.Success)
        {
            _cart.Clear();
            _currentView = "orders";
        }
    }

    private bool IsStaff() => _currentUser?.Role is UserRole.Admin or UserRole.Librarian;

    private void HandleCreateBook(JsonElement root)
    {
        if (!IsStaff()) { MessageBox.Show("Không có quyền."); return; }
        var book = ReadBookFromJson(root, null);
        if (book is null) return;
        _repository.CreateBook(book);
        MessageBox.Show($"Đã thêm sách: {book.Name}");
    }

    private void HandleUpdateBook(JsonElement root)
    {
        if (!IsStaff()) { MessageBox.Show("Không có quyền."); return; }
        if (!root.TryGetProperty("id", out var idEl)) return;
        var existing = _repository.GetBook(idEl.GetInt32());
        if (existing is null) { MessageBox.Show("Không tìm thấy sách."); return; }
        var updated = ReadBookFromJson(root, existing);
        if (updated is null) return;
        _repository.UpdateBook(updated);
        MessageBox.Show($"Đã cập nhật sách: {updated.Name}");
    }

    private void HandleDeleteBook(JsonElement root)
    {
        if (!IsStaff()) { MessageBox.Show("Không có quyền."); return; }
        if (!root.TryGetProperty("id", out var idEl)) return;
        try
        {
            _repository.DeleteBook(idEl.GetInt32());
            MessageBox.Show("Đã xoá sách.");
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Không thể xoá");
        }
    }

    private static Book? ReadBookFromJson(JsonElement root, Book? existing)
    {
        string Str(string k) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        int Int(string k, int fallback = 0) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;
        decimal Dec(string k, decimal fallback = 0) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : fallback;

        var name = Str("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Tên sách không được trống.");
            return null;
        }

        var book = existing ?? new Book();
        book.Name = name;
        book.Author = Str("author");
        book.Category = Str("category");
        book.Summary = Str("summary");
        book.CoverImageUrl = string.IsNullOrWhiteSpace(Str("coverImageUrl")) ? "images/books/img-01.jpg" : Str("coverImageUrl");
        book.InventoryCount = Math.Max(0, Int("inventoryCount"));
        book.BasePrice = Math.Max(0, Dec("basePrice"));
        book.DiscountPercent = Math.Clamp(Int("discountPercent", 5), 0, 50);
        book.NumberPage = Math.Max(0, Int("numberPage"));
        book.DatePublication = Str("datePublication");
        return book;
    }

    private void ExportInvoicePdf(int orderId)
    {
        var order = _repository.GetOrders().FirstOrDefault(o => o.Id == orderId);
        if (order is null) { MessageBox.Show("Không tìm thấy đơn."); return; }
        var user = _repository.GetUsers().FirstOrDefault(u => u.Id == order.UserId);
        if (user is null) { MessageBox.Show("Không tìm thấy người dùng."); return; }

        var bytes = PdfExporter.GenerateInvoice(order, user, _repository.GetBooks());
        SavePdfAndOpen(bytes, $"HoaDon_{orderId:D5}.pdf");
    }

    private void ExportAnalyticsPdf()
    {
        var bytes = PdfExporter.GenerateAnalyticsReport(
            _repository.GetBooks(), _repository.GetOrders(), _repository.GetUsers());
        SavePdfAndOpen(bytes, $"BaoCao_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    private static void SavePdfAndOpen(byte[] bytes, string defaultName)
    {
        var dlg = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = "PDF files|*.pdf",
            DefaultExt = ".pdf"
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllBytes(dlg.FileName, bytes);
        try
        {
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch { /* nếu không có default pdf viewer thì chỉ save */ }
        MessageBox.Show($"Đã lưu PDF:\n{dlg.FileName}", "Xuất PDF thành công");
    }

    private void HandleSubmitReview(JsonElement root)
    {
        if (_currentUser is null)
        {
            _currentView = "login";
            return;
        }

        var bookId = root.TryGetProperty("bookId", out var b) ? b.GetInt32() : 0;
        var rating = root.TryGetProperty("rating", out var r) ? r.GetInt32() : 5;
        var content = root.TryGetProperty("content", out var cn) ? cn.GetString() ?? "" : "";
        if (bookId == 0) return;

        _repository.AddReview(new BookReview
        {
            BookId = bookId,
            UserId = _currentUser.Id,
            ReviewerName = _currentUser.FullName,
            Rating = rating,
            Content = content,
            CreatedAt = DateTime.UtcNow
        });
        _detailBookId = bookId;
        _currentView = "detail";
    }

    private const string SpaRendererScript = @"
(function() {
  window.esc=function(s){if(s==null)return'';return String(s).replace(/[&<>]/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;'}[c];});};
  window.money=function(n){try{return Number(n).toLocaleString('vi-VN')+' đ';}catch(e){return n+'';};};
  window.host=function(d){window.callHost(d);};
  window.go=function(v){window.host({action:'nav',target:v});};
  function esc(s){return window.esc(s);}
  function money(n){return window.money(n);}
  function host(d){window.host(d);}
  function go(v){window.go(v);}
  function bellHtml(){
    var s=window.appState||{};
    if(!s.user)return'';
    var items=s.notifications||[];
    var unread=items.filter(function(n){return!n.isRead;}).length;
    var list=items.length?items.map(function(n){
      var cls='notif-item'+(n.isRead?'':' unread')+' notif-kind-'+(n.kind||'info');
      var d=new Date(n.sentAt);
      return'<div class=""'+cls+'"" onclick=""window.doMarkRead('+n.id+')"">'+esc(n.message)+'<div class=""meta"">'+d.toLocaleString('vi-VN')+'</div></div>';
    }).join(''):'<div style=""padding:20px;text-align:center;color:var(--muted);font-size:13px;"">Chưa có thông báo</div>';
    return'<div style=""position:relative;"" id=""bellWrap"">'+
      '<button class=""icon-btn"" onclick=""window.toggleBell(event)""><span>🔔</span>'+
      (unread>0?'<span class=""notif-badge"">'+unread+'</span>':'')+
      '</button>'+
      '<div class=""notif-dropdown"" id=""bellDrop"">'+
        '<div class=""notif-head""><span>Thông báo</span>'+(unread>0?'<a onclick=""window.doMarkAllRead(event)"" style=""font-size:12px;cursor:pointer;color:var(--primary);"">Đánh dấu đọc hết</a>':'')+'</div>'+
        list+
      '</div>'+
    '</div>';
  }
  function darkBtn(){
    var isDark=document.body.classList.contains('dark');
    return'<button class=""icon-btn"" onclick=""window.toggleDark()"" title=""Chuyển chế độ"">'+(isDark?'☀️':'🌙')+'</button>';
  }
  function navBar(){
    var s=window.appState||{};
    var li=!!s.user,role=s.role||'';
    var left=li?('<span style=""margin-right:8px;font-size:13px;opacity:.85;"">Xin chào <b>'+esc(s.user.fullName)+'</b></span>'):'';
    var links='';
    if(!li){
      links='<a onclick=""window.doNav(\'login\')"">Đăng nhập</a><a onclick=""window.doNav(\'signup\')"">Đăng ký</a>';
    } else if(role==='User'){
      links='<a onclick=""window.doNav(\'home\')"">🏠 Trang chủ</a>'+
            '<a onclick=""window.doNav(\'products\')"">📚 Sách</a>'+
            '<a onclick=""window.doNav(\'cart\')"">🛒 Giỏ ('+(s.cart||[]).length+')</a>'+
            '<a onclick=""window.doNav(\'orders\')"">📋 Đơn của tôi</a>'+
            '<a onclick=""window.doNav(\'achievements\')"">🏆 Thành tích</a>'+
            '<a onclick=""window.doNav(\'profile\')"">👤 Cá nhân</a>'+
            '<a onclick=""window.doLogout()"">Đăng xuất</a>';
    } else if(role==='Librarian'){
      links='<a onclick=""window.doNav(\'librarian\')"">📋 Dashboard</a>'+
            '<a onclick=""window.doNav(\'analytics\')"">📊 Analytics</a>'+
            '<a onclick=""window.doNav(\'books\')"">📚 Sách</a>'+
            '<a onclick=""window.doLogout()"">Đăng xuất</a>';
    } else if(role==='Admin'){
      links='<a onclick=""window.doNav(\'admin\')"">⚙️ Admin</a>'+
            '<a onclick=""window.doNav(\'analytics\')"">📊 Analytics</a>'+
            '<a onclick=""window.doNav(\'books\')"">📚 Sách</a>'+
            '<a onclick=""window.doNav(\'users\')"">👥 Users</a>'+
            '<a onclick=""window.doLogout()"">Đăng xuất</a>';
    }
    return '<header><div class=""logo"">📚 Thư viện sách</div>'+
      '<div style=""display:flex;align-items:center;gap:8px;"">'+left+darkBtn()+bellHtml()+'</div>'+
      '<nav class=""nav-links"">'+links+'</nav></header>';
  }
  function bookCard(b){
    var eff=b.borrowCount>10?Math.round(b.basePrice*(1-Math.max(5,Math.min(10,b.discountPercent))/100)):b.basePrice;
    var disc=eff!==b.basePrice;
    var buyP=Math.round(eff*9.5);
    return '<div class=""book-card"">'+
      '<img class=""book-img"" src=""'+esc(b.coverImageUrl)+'"" onerror=""this.src=\'images/books/img-01.jpg\'""/>'+
      '<div class=""book-body"">'+
        '<div class=""book-name"">'+esc(b.name)+'</div>'+
        '<div class=""book-meta"">'+esc(b.author)+' &bull; '+esc(b.category)+'</div>'+
        '<div class=""book-meta"">Còn: '+b.inventoryCount+'</div>'+
        '<div style=""margin:4px 0;"">'+
          (disc?'<span class=""price-old"">'+money(b.basePrice)+'</span> <span class=""price"">'+money(eff)+'</span> <span class=""badge badge-yellow"">-'+Math.max(5,Math.min(10,b.discountPercent))+'%</span>':'<span class=""price"" style=""color:#1E88E5;"">'+money(eff)+'</span>')+
        '</div>'+
        '<div style=""background:#F8FBFF;border-radius:6px;padding:6px 8px;margin:4px 0;font-size:12px;"">'+
          '<label style=""cursor:pointer;margin-right:10px;""><input type=""radio"" name=""btype_'+b.id+'"" value=""borrow"" checked> Mượn ('+money(eff)+')</label>'+
          '<label style=""cursor:pointer;""><input type=""radio"" name=""btype_'+b.id+'"" value=""buy""> Mua ('+money(buyP)+')</label>'+
        '</div>'+
        '<div class=""book-actions"">'+
          '<button class=""btn btn-primary"" onclick=""window.doDetail('+b.id+')"">Chi tiết</button>'+
          '<button class=""btn btn-yellow"" onclick=""window.doAdd('+b.id+',1,window.getBType('+b.id+'))"">+ Giỏ</button>'+
        '</div>'+
      '</div>'+
    '</div>';
  }
  window.doLogin=function(){var u=document.getElementById('u_user'),p=document.getElementById('u_pass');if(u&&p)window.callHost({action:'login',username:u.value,password:p.value});};
  window.doSignup=function(){var u=document.getElementById('s_user'),p=document.getElementById('s_pass'),f=document.getElementById('s_name'),e=document.getElementById('s_email'),ph=document.getElementById('s_phone'),a=document.getElementById('s_addr');window.callHost({action:'signup',username:u?u.value:'',password:p?p.value:'',fullname:f?f.value:'',email:e?e.value:'',phone:ph?ph.value:'',address:a?a.value:''});};
  window.doLogout=function(){window.callHost({action:'logout'});};
  window.doNav=function(v){window.callHost({action:'nav',target:v});};
  window.doDetail=function(id){window.callHost({action:'detail',bookId:id});};
  window.doAdd=function(id,qty){window.callHost({action:'addToCart',bookId:id,quantity:qty||1});};
  window.doRemove=function(id){window.callHost({action:'removeCart',bookId:id});};
  window.doQty=function(id,v){window.callHost({action:'updateQty',bookId:id,quantity:parseInt(v)||0});};
  window.doCheckout=function(){var c=document.getElementById('couponInput');window.callHost({action:'checkout',coupon:c?c.value:''});};
  window.doReview=function(){var r=document.getElementById('rv_star'),c=document.getElementById('rv_content');window.callHost({action:'submitReview',bookId:(window.appState||{}).detailBookId,rating:r?parseInt(r.value):5,content:c?c.value:''});};
  window.doReminder=function(){window.callHost({action:'reminder'});};
  window.doDeleteOrder=function(id){if(confirm('Xoa don #'+id+'?'))window.callHost({action:'deleteOrder',orderId:id});};
  window.doGoogleLogin=function(){window.callHost({action:'googleLogin'});};
  window.doSearch=function(){var q=document.getElementById('searchBox').value.toLowerCase();document.querySelectorAll('.book-card').forEach(function(c){c.style.display=c.innerText.toLowerCase().indexOf(q)>=0?'':'none';});};
  window.getBType=function(id){var r=document.querySelector('input[name=""btype_'+id+'""]:checked');return r?r.value==='buy':false;};
  window.toggleDark=function(){var b=document.body;b.classList.toggle('dark');try{localStorage.setItem('dark',b.classList.contains('dark')?'1':'0');}catch(e){}window.renderApp();if(window.__currentView==='analytics'&&window.renderCharts)setTimeout(window.renderCharts,50);};
  window.toggleBell=function(ev){if(ev){ev.stopPropagation();}var d=document.getElementById('bellDrop');if(d)d.classList.toggle('show');};
  window.doMarkRead=function(id){window.callHost({action:'markNotifRead',notifId:id});};
  window.doMarkAllRead=function(ev){if(ev)ev.stopPropagation();window.callHost({action:'markAllNotifRead'});};
  window.doExportInvoice=function(id){window.callHost({action:'exportInvoicePdf',orderId:id});};
  window.doExportAnalytics=function(){window.callHost({action:'exportAnalyticsPdf'});};
  document.addEventListener('click',function(e){
    var w=document.getElementById('bellWrap');
    if(w&&!w.contains(e.target)){var d=document.getElementById('bellDrop');if(d)d.classList.remove('show');}
  });

  function pgLogin(){
    return '<div class=""login-wrap""><div class=""login-card"">'+
      '<h2>Đăng nhập</h2><p>Đăng nhập để mượn sách và theo dõi đơn</p>'+
      '<div class=""hint-box"">Tài khoản test:<br/>admin / admin123 &nbsp;|&nbsp; librarian / lib123 &nbsp;|&nbsp; user / user123</div>'+
      '<div class=""form-group""><label>Tên đăng nhập</label><input id=""u_user"" placeholder=""Username hoặc Email""></div>'+
      '<div class=""form-group""><label>Mật khẩu</label><input id=""u_pass"" type=""password"" placeholder=""Password""></div>'+
      '<button class=""btn btn-primary btn-full"" onclick=""window.doLogin()"">Đăng nhập</button>'+
      '<button class=""btn btn-yellow btn-full"" style=""margin-top:10px;"" onclick=""window.doGoogleLogin()"">🔵 Đăng nhập với Google</button>'+
      '<p style=""text-align:center;margin-top:16px;font-size:14px;"">Chưa có tài khoản? <a onclick=""window.doNav(\'signup\')"" style=""color:#1E88E5;cursor:pointer;font-weight:600;"">Đăng ký</a></p>'+
    '</div></div>';
  }
  function pgSignup(){
    return '<div class=""login-wrap""><div class=""login-card"" style=""width:500px;"">'+
      '<h2>Đăng ký tài khoản</h2>'+
      '<div class=""form-group""><label>Username</label><input id=""s_user"" placeholder=""Username""></div>'+
      '<div class=""form-group""><label>Mật khẩu</label><input id=""s_pass"" type=""password"" placeholder=""Password""></div>'+
      '<div class=""form-group""><label>Họ tên</label><input id=""s_name"" placeholder=""Họ và tên""></div>'+
      '<div class=""form-group""><label>Email</label><input id=""s_email"" type=""email"" placeholder=""Email""></div>'+
      '<div class=""form-group""><label>Điện thoại</label><input id=""s_phone"" placeholder=""Số điện thoại""></div>'+
      '<div class=""form-group""><label>Địa chỉ</label><input id=""s_addr"" placeholder=""Địa chỉ""></div>'+
      '<button class=""btn btn-primary btn-full"" onclick=""window.doSignup()"">Đăng ký</button>'+
      '<p style=""text-align:center;margin-top:16px;font-size:14px;"">Đã có tài khoản? <a onclick=""window.doNav(\'login\')"" style=""color:#1E88E5;cursor:pointer;font-weight:600;"">Đăng nhập</a></p>'+
    '</div></div>';
  }
  function pgHome(){
    var s=window.appState,top=(s.books||[]).slice().sort(function(a,b){return b.numberSold-a.numberSold;}).slice(0,8);
    var rec=(s.recommendations||[]).slice(0,6);
    var recHtml=rec.length?'<div class=""recommend-strip""><div class=""section-title"" style=""margin-top:0;"">🎯 Dành riêng cho bạn</div>'+
      '<div style=""font-size:12px;color:var(--muted);margin-bottom:10px;"">Dựa trên sở thích và các sách bạn đã mượn/mua</div>'+
      '<div class=""card-grid"">'+rec.map(bookCard).join('')+'</div></div>':'';
    return '<div class=""page"">'+
      '<div class=""hero""><h2>Chào mừng '+esc((s.user||{}).fullName||'')+' đến thư viện sách</h2>'+
      '<p>Khám phá '+(s.books||[]).length+' đầu sách hay. Giảm 5-10% cho sách được mượn trên 10 lần!</p>'+
      '<button class=""btn btn-yellow"" onclick=""window.doNav(\'products\')"">Xem tất cả sách →</button></div>'+
      recHtml+
      '<div class=""section-title"">📈 Bán chạy nhất</div>'+
      '<div class=""card-grid"">'+top.map(bookCard).join('')+'</div></div>';
  }
  window.__filter={q:'',cat:'',min:'',max:'',stock:false,disc:false,sort:'default'};
  window.applyFilter=function(){
    var f=window.__filter;
    f.q=(document.getElementById('f_q')||{value:''}).value.toLowerCase();
    f.cat=(document.getElementById('f_cat')||{value:''}).value;
    f.min=(document.getElementById('f_min')||{value:''}).value;
    f.max=(document.getElementById('f_max')||{value:''}).value;
    f.stock=(document.getElementById('f_stock')||{checked:false}).checked;
    f.disc=(document.getElementById('f_disc')||{checked:false}).checked;
    f.sort=(document.getElementById('f_sort')||{value:'default'}).value;
    var list=document.getElementById('productList');
    if(list)list.innerHTML=window.filteredBookCards();
  };
  window.resetFilter=function(){window.__filter={q:'',cat:'',min:'',max:'',stock:false,disc:false,sort:'default'};window.renderApp();};
  window.filteredBookCards=function(){
    var s=window.appState,f=window.__filter;
    var items=(s.books||[]).slice();
    if(f.q)items=items.filter(function(b){return(b.name+' '+b.author).toLowerCase().indexOf(f.q)>=0;});
    if(f.cat)items=items.filter(function(b){return b.category===f.cat;});
    if(f.min!=='')items=items.filter(function(b){return b.basePrice>=Number(f.min);});
    if(f.max!=='')items=items.filter(function(b){return b.basePrice<=Number(f.max);});
    if(f.stock)items=items.filter(function(b){return b.inventoryCount>0;});
    if(f.disc)items=items.filter(function(b){return b.borrowCount>10;});
    switch(f.sort){
      case'price_asc':items.sort(function(a,b){return a.basePrice-b.basePrice;});break;
      case'price_desc':items.sort(function(a,b){return b.basePrice-a.basePrice;});break;
      case'sold':items.sort(function(a,b){return b.numberSold-a.numberSold;});break;
      case'borrow':items.sort(function(a,b){return b.borrowCount-a.borrowCount;});break;
      case'name':items.sort(function(a,b){return a.name.localeCompare(b.name);});break;
    }
    return items.length?items.map(bookCard).join(''):'<div style=""grid-column:1/-1;text-align:center;padding:40px;color:var(--muted);"">Không có sách phù hợp. <a onclick=""window.resetFilter()"" style=""color:var(--primary);cursor:pointer;"">Xoá bộ lọc</a></div>';
  };
  function pgProducts(){
    var s=window.appState,f=window.__filter;
    var cats=Array.from(new Set((s.books||[]).map(function(b){return b.category;}).filter(function(x){return x;}))).sort();
    var catOpts='<option value="""">Tất cả danh mục</option>'+cats.map(function(c){return'<option value=""'+esc(c)+'""'+(f.cat===c?' selected':'')+'>'+esc(c)+'</option>';}).join('');
    var sortOpts=[['default','Mặc định'],['name','Tên A-Z'],['price_asc','Giá thấp → cao'],['price_desc','Giá cao → thấp'],['sold','Bán chạy nhất'],['borrow','Mượn nhiều nhất']].map(function(o){return'<option value=""'+o[0]+'""'+(f.sort===o[0]?' selected':'')+'>'+o[1]+'</option>';}).join('');
    return '<div class=""page""><div class=""section-title"">📚 Tất cả sách</div>'+
      '<div style=""background:var(--card);border-radius:10px;padding:14px;box-shadow:var(--shadow);margin-bottom:16px;"">'+
        '<div style=""display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px;"">'+
          '<input id=""f_q"" placeholder=""🔎 Tên sách, tác giả..."" value=""'+esc(f.q)+'"" oninput=""window.applyFilter()"">'+
          '<select id=""f_cat"" onchange=""window.applyFilter()"">'+catOpts+'</select>'+
          '<input id=""f_min"" type=""number"" placeholder=""Giá tối thiểu"" value=""'+esc(f.min)+'"" oninput=""window.applyFilter()"">'+
          '<input id=""f_max"" type=""number"" placeholder=""Giá tối đa"" value=""'+esc(f.max)+'"" oninput=""window.applyFilter()"">'+
          '<select id=""f_sort"" onchange=""window.applyFilter()"">'+sortOpts+'</select>'+
          '<button class=""btn btn-yellow"" onclick=""window.resetFilter()"">Xoá lọc</button>'+
        '</div>'+
        '<div style=""margin-top:10px;font-size:13px;display:flex;gap:16px;flex-wrap:wrap;"">'+
          '<label style=""cursor:pointer;""><input id=""f_stock"" type=""checkbox""'+(f.stock?' checked':'')+' onchange=""window.applyFilter()"" style=""margin-right:4px;"">Chỉ còn hàng</label>'+
          '<label style=""cursor:pointer;""><input id=""f_disc"" type=""checkbox""'+(f.disc?' checked':'')+' onchange=""window.applyFilter()"" style=""margin-right:4px;"">Đang giảm giá (mượn &gt; 10 lần)</label>'+
        '</div>'+
      '</div>'+
      '<div id=""productList"" class=""card-grid"">'+window.filteredBookCards()+'</div></div>';
  }
  function pgDetail(){
    var s=window.appState,b=(s.books||[]).find(function(x){return x.id===s.detailBookId;});
    if(!b)return'<div class=""page"">Không tìm thấy sách.</div>';
    var eff=b.borrowCount>10?Math.round(b.basePrice*(1-Math.max(5,Math.min(10,b.discountPercent))/100)):b.basePrice;
    return '<div class=""page""><div class=""detail-grid"">'+
      '<img src=""'+esc(b.coverImageUrl)+'"" onerror=""this.src=\'images/books/img-01.jpg\'"" style=""width:100%;border-radius:10px;box-shadow:0 4px 14px rgba(0,0,0,0.12);""/>'+
      '<div>'+
        '<h2 style=""color:#1565C0;"">'+esc(b.name)+'</h2>'+
        '<p><b>Tác giả:</b> '+esc(b.author)+' &bull; <b>Danh mục:</b> '+esc(b.category)+'</p>'+
        '<p><b>Xuất bản:</b> '+esc(b.datePublication)+' &bull; <b>Số trang:</b> '+b.numberPage+'</p>'+
        '<p><b>Tồn kho:</b> '+b.inventoryCount+' &bull; <b>Đã bán:</b> '+b.numberSold+'</p>'+
        '<div style=""margin:12px 0;""><span class=""price"">'+money(eff)+'</span>'+(eff!==b.basePrice?' <span class=""price-old"">'+money(b.basePrice)+'</span> <span class=""badge badge-yellow"">-'+Math.max(5,Math.min(10,b.discountPercent))+'%</span>':'')+'</div>'+
        '<p style=""color:#555;line-height:1.6;"">'+esc(b.summary)+'</p>'+
        '<div style=""background:#F8FBFF;border-radius:6px;padding:8px 12px;margin:12px 0;font-size:13px;"">'+
          '<b>Chọn hình thức:</b><br/>'+
          '<label style=""cursor:pointer;margin-right:16px;""><input type=""radio"" name=""btype_d"" value=""borrow"" checked> Mượn ('+money(eff)+')</label>'+
          '<label style=""cursor:pointer;""><input type=""radio"" name=""btype_d"" value=""buy""> Mua luôn ('+money(Math.round(eff*9.5))+')</label>'+
        '</div>'+
        '<div style=""display:flex;gap:8px;margin:12px 0;""><input id=""qtyInput"" type=""number"" min=""1"" value=""1"" style=""width:80px;""><button class=""btn btn-primary"" onclick=""window.doAdd('+b.id+',parseInt(document.getElementById(\'qtyInput\').value)||1,window.getBType(\'d\'))"">Thêm vào giỏ</button></div>'+
        '<div class=""section-title"" style=""font-size:15px;"">⭐ Đánh giá</div>'+
        (b.reviews||[]).map(function(r){return'<div class=""review-item""><b>'+esc(r.reviewerName)+'</b> <span class=""badge badge-yellow"">★'+r.rating+'</span><p style=""margin:4px 0 0;"">'+esc(r.content)+'</p></div>';}).join('')+
        (s.user?'<div style=""margin-top:12px;""><div class=""form-group""><label>Đánh giá của bạn (sao)</label><select id=""rv_star""><option value=""5"">★★★★★</option><option value=""4"">★★★★</option><option value=""3"">★★★</option><option value=""2"">★★</option><option value=""1"">★</option></select></div><div class=""form-group""><label>Nội dung</label><textarea id=""rv_content"" rows=""3"" placeholder=""Viết nhận xét...""></textarea></div><button class=""btn btn-primary"" onclick=""window.doReview()"">Gửi đánh giá</button></div>':'')+
      '</div></div>'+
      ((s.similar||[]).length?'<div class=""recommend-strip"" style=""margin-top:20px;""><div class=""section-title"" style=""margin-top:0;"">🔗 Người đọc cuốn này cũng thích</div><div class=""card-grid"">'+(s.similar||[]).map(bookCard).join('')+'</div></div>':'')+
      '</div>';
  }
  function pgCart(){
    var s=window.appState,cart=s.cart||[],total=cart.reduce(function(sum,c){return sum+c.effectivePrice*c.quantity;},0);
    if(!cart.length)return'<div class=""page""><div class=""section-title"">🛒 Giỏ hàng</div><p>Giỏ hàng trống. <a onclick=""window.doNav(\'products\')"" style=""color:#1E88E5;cursor:pointer;"">Xem sách →</a></p></div>';
    var rows=cart.map(function(c){
      var typeBadge=c.isPurchase?'<span class=""badge badge-yellow"">Mua</span>':'<span class=""badge badge-blue"">Mượn</span>';
      return'<tr>'+
        '<td style=""padding:10px;""><div style=""display:flex;align-items:center;gap:10px;""><img src=""'+esc(c.cover)+'"" onerror=""this.src=\'images/books/img-01.jpg\'"" style=""width:56px;height:56px;object-fit:cover;border-radius:6px;flex-shrink:0;""/><div><div>'+esc(c.name)+'</div><div style=""margin-top:4px;"">'+typeBadge+'</div></div></div></td>'+
        '<td style=""text-align:center;white-space:nowrap;"">'+money(c.effectivePrice)+'</td>'+
        '<td style=""text-align:center;""><input type=""number"" min=""1"" value=""'+c.quantity+'"" style=""width:60px;text-align:center;"" onchange=""window.doQty('+c.bookId+',this.value)""></td>'+
        '<td style=""text-align:center;font-weight:600;"">'+money(c.effectivePrice*c.quantity)+'</td>'+
        '<td style=""text-align:center;""><button class=""btn btn-red btn-sm"" onclick=""window.doRemove('+c.bookId+')"">Xóa</button></td>'+
      '</tr>';}).join('');
    return'<div class=""page""><div class=""section-title"">🛒 Giỏ hàng</div>'+
      '<div style=""overflow-x:auto;""><table style=""table-layout:fixed;"">'+
        '<colgroup><col style=""width:45%""/><col style=""width:15%""/><col style=""width:12%""/><col style=""width:15%""/><col style=""width:13%""/></colgroup>'+
        '<thead><tr><th>Sách</th><th style=""text-align:center;"">Đơn giá</th><th style=""text-align:center;"">SL</th><th style=""text-align:center;"">Thành tiền</th><th></th></tr></thead>'+
        '<tbody>'+rows+'</tbody>'+
      '</table></div>'+
      '<div style=""background:white;padding:20px;border-radius:10px;margin-top:16px;box-shadow:0 2px 8px rgba(0,0,0,0.07);display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:12px;"">'+
        '<div style=""font-size:20px;font-weight:700;color:#1565C0;"">Tổng: '+money(total)+'</div>'+
        '<div style=""display:flex;gap:8px;flex:1;max-width:400px;""><input id=""couponInput"" placeholder=""Mã giảm giá (SAVE10, WELCOME5)""><button class=""btn btn-primary"" onclick=""window.doCheckout()"">Thanh toán</button></div>'+
      '</div></div>';
  }
  function pgOrders(){
    var s=window.appState,orders=(s.orders||[]).filter(function(o){return o.userId===s.user.id;});
    return'<div class=""page""><div class=""section-title"">📋 Đơn của tôi</div>'+
      (orders.length?orders.map(function(o){
        var st=o.status,badge=st==='Borrowing'?'badge-blue':st==='Overdue'?'badge-red':st==='Returned'?'badge-green':'badge-yellow';
        return'<div style=""background:var(--card);border-radius:10px;padding:16px;margin-bottom:12px;box-shadow:var(--shadow);"">'+
          '<div style=""display:flex;justify-content:space-between;margin-bottom:8px;align-items:center;gap:8px;flex-wrap:wrap;""><b>Đơn #'+o.id+'</b> <div><span class=""badge '+badge+'"">'+esc(st)+'</span> <button class=""btn btn-yellow btn-sm"" onclick=""window.doExportInvoice('+o.id+')"">📄 Xuất PDF</button></div></div>'+
          '<div style=""color:var(--muted);font-size:13px;"">'+(o.status==='Purchased'?'Ngày mua: '+new Date(o.borrowDate).toLocaleDateString():'Mượn: '+new Date(o.borrowDate).toLocaleDateString()+' &bull; Hạn: '+new Date(o.dueDate).toLocaleDateString())+'</div>'+
          '<div style=""font-weight:600;margin:8px 0;"">Tổng: '+money(o.totalAmount)+(o.lateFeeAmount>0?' <span style=""color:#E53935;"">+ Phí trễ: '+money(o.lateFeeAmount)+'</span>':'')+'</div>'+
          '<ul style=""margin:0;padding-left:20px;color:var(--muted);font-size:13px;"">'+( o.items||[]).map(function(i){return'<li>'+esc(i.bookName)+' x '+i.quantity+' <span class=""badge '+(i.isPurchase?'badge-yellow"">Mua luôn':'badge-blue"">Mượn')+'</span></li>';}).join('')+'</ul>'+
        '</div>';}).join(''):'<p>Bạn chưa có đơn nào. <a onclick=""window.doNav(\'products\')"" style=""color:var(--primary);cursor:pointer;"">Xem sách →</a></p>')+
    '</div>';
  }
  function pgProfile(){
    var s=window.appState,u=s.user;if(!u)return'<div class=""page"">Vui lòng đăng nhập.</div>';
    var a=s.achievements||[],unlocked=a.filter(function(x){return x.unlocked;}).length;
    var mini=a.slice(0,4).map(function(x){return'<div class=""achv-card '+(x.unlocked?'unlocked':'')+'"" style=""padding:10px;""><div class=""achv-icon"" style=""font-size:28px;"">'+x.icon+'</div><div class=""achv-title"" style=""font-size:12px;"">'+esc(x.title)+'</div></div>';}).join('');
    return'<div class=""page""><div style=""display:grid;grid-template-columns:1fr 1fr;gap:16px;"" class=""profile-grid"">'+
      '<div style=""background:var(--card);border-radius:10px;padding:24px;box-shadow:var(--shadow);"">'+
      '<div class=""section-title"" style=""margin-top:0;"">👤 Thông tin cá nhân</div>'+
      '<table style=""width:auto;box-shadow:none;""><tbody>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Họ tên</td><td>'+esc(u.fullName)+'</td></tr>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Username</td><td>'+esc(u.username)+'</td></tr>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Email</td><td>'+esc(u.email)+'</td></tr>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Điện thoại</td><td>'+esc(u.phone)+'</td></tr>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Địa chỉ</td><td>'+esc(u.address)+'</td></tr>'+
      '<tr><td style=""font-weight:600;padding:6px 16px 6px 0;"">Vai trò</td><td><span class=""badge badge-blue"">'+esc(u.role)+'</span></td></tr>'+
      '</tbody></table></div>'+
      '<div style=""background:var(--card);border-radius:10px;padding:24px;box-shadow:var(--shadow);"">'+
      '<div class=""section-title"" style=""margin-top:0;justify-content:space-between;""><span>🏆 Thành tích ('+unlocked+'/'+a.length+')</span>'+
      '<a onclick=""window.doNav(\'achievements\')"" style=""font-size:12px;color:var(--primary);cursor:pointer;font-weight:500;"">Xem tất cả →</a></div>'+
      '<div style=""display:grid;grid-template-columns:repeat(2,1fr);gap:10px;"">'+mini+'</div>'+
      '</div></div></div>';
  }
  function pgLibrarian(){
    var s=window.appState,sorted=(s.orders||[]).filter(function(o){return o.status==='Borrowing'||o.status==='Overdue';}).slice().sort(function(a,b){return new Date(a.dueDate)-new Date(b.dueDate);});
    return'<div class=""page""><div class=""section-title"">📋 Quản lý đến hạn trả</div>'+
      '<button class=""btn btn-primary"" style=""margin-bottom:16px;"" onclick=""window.doReminder()"">📧 Gửi nhắc hằng ngày + Tính lãi 3%/ngày</button>'+
      '<table><thead><tr><th>#</th><th>User</th><th>Email</th><th>Hạn trả</th><th>Trạng thái</th><th>Tổng</th><th>Phí trễ</th></tr></thead><tbody>'+
      sorted.map(function(o){
        var badge=o.status==='Overdue'?'badge-red':'badge-yellow';
        return'<tr><td>'+o.id+'</td><td>'+esc(o.userName)+'</td><td>'+esc(o.userEmail)+'</td><td>'+new Date(o.dueDate).toLocaleDateString()+'</td><td><span class=""badge '+badge+'"">'+esc(o.status)+'</span></td><td>'+money(o.totalAmount)+'</td><td style=""color:#E53935;font-weight:600;"">'+money(o.lateFeeAmount)+'</td></tr>';}).join('')+
      '</tbody></table><div class=""section-title"" style=""margin-top:24px;"">📚 Tồn kho sách</div>'+
      '<table><thead><tr><th>Ảnh</th><th>Tên</th><th>Tác giả</th><th>Tồn kho</th><th>Lượt mượn</th><th>Đã bán</th></tr></thead><tbody>'+
      (s.books||[]).map(function(b){return'<tr><td><img src=""'+esc(b.coverImageUrl)+'"" onerror=""this.src=\'images/books/img-01.jpg\'"" style=""width:48px;height:48px;object-fit:cover;border-radius:4px;""/></td><td>'+esc(b.name)+'</td><td>'+esc(b.author)+'</td><td>'+b.inventoryCount+'</td><td>'+b.borrowCount+'</td><td>'+b.numberSold+'</td></tr>';}).join('')+
      '</tbody></table></div>';
  }
  function pgAdmin(){
    var s=window.appState;
    var ords=s.orders||[],books=s.books||[],users=s.users||[];
    var ov=(ords.filter(function(o){return o.lateFeeAmount>0;}).reduce(function(a,o){return a+o.lateFeeAmount;},0));
    return'<div class=""page"">'+
      '<div class=""section-title"">⚙️ Admin Dashboard</div>'+
      '<div class=""stat-cards"">'+
        '<div class=""stat-card""><div class=""stat-number"">'+ords.length+'</div><div class=""stat-label"">Tổng đơn</div></div>'+
        '<div class=""stat-card yellow""><div class=""stat-number"">'+books.length+'</div><div class=""stat-label"">Đầu sách</div></div>'+
        '<div class=""stat-card""><div class=""stat-number"">'+users.length+'</div><div class=""stat-label"">Người dùng</div></div>'+
        '<div class=""stat-card red""><div class=""stat-number"">'+money(ov)+'</div><div class=""stat-label"">Tổng phí trễ</div></div>'+
      '</div>'+
      '<div class=""section-title"">📋 Tất cả đơn</div>'+
      '<table><thead><tr><th>#</th><th>User</th><th>Email</th><th>Ngày mượn</th><th>Hạn</th><th>TT</th><th>Tổng</th><th>Phí trễ</th><th></th></tr></thead><tbody>'+
      ords.map(function(o){
        var badge=o.status==='Overdue'?'badge-red':o.status==='Returned'?'badge-green':o.status==='Borrowing'?'badge-blue':'badge-yellow';
        return'<tr><td>'+o.id+'</td><td>'+esc(o.userName)+'</td><td>'+esc(o.userEmail)+'</td><td>'+new Date(o.borrowDate).toLocaleDateString()+'</td><td>'+new Date(o.dueDate).toLocaleDateString()+'</td><td><span class=""badge '+badge+'"">'+esc(o.status)+'</span></td><td>'+money(o.totalAmount)+'</td><td style=""color:#E53935;"">'+money(o.lateFeeAmount)+'</td><td><button class=""btn btn-red btn-sm"" onclick=""window.doDeleteOrder('+o.id+')"">Xóa</button></td></tr>';}).join('')+
      '</tbody></table>'+
      '<div class=""section-title"" style=""margin-top:24px;"">📦 Chi tiết từng đơn</div>'+
      (ords.length?'<table><thead><tr><th>Đơn #</th><th>Sách</th><th>SL</th><th>Đơn giá</th><th>Loại</th></tr></thead><tbody>'+
      ords.flatMap(function(o){return(o.items||[]).map(function(i){return'<tr><td>#'+o.id+'</td><td>'+esc(i.bookName)+'</td><td>'+i.quantity+'</td><td>'+money(i.unitPrice)+'</td><td><span class=""badge '+(i.isPurchase?'badge-yellow"">Mua luôn':'badge-blue"">Mượn')+'</span></td></tr>';});}).join('')+
      '</tbody></table>':'')+
      '<div class=""section-title"" style=""margin-top:24px;"">👥 Người dùng (không lộ mật khẩu)</div>'+
      '<table><thead><tr><th>#</th><th>Username</th><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Phone</th></tr></thead><tbody>'+
      users.map(function(u){return'<tr><td>'+u.id+'</td><td>'+esc(u.username)+'</td><td>'+esc(u.fullName)+'</td><td>'+esc(u.email)+'</td><td><span class=""badge badge-blue"">'+esc(u.role)+'</span></td><td>'+esc(u.phone)+'</td></tr>';}).join('')+
      '</tbody></table></div>';
  }
  function pgAchievements(){
    var s=window.appState,a=s.achievements||[];
    var unlocked=a.filter(function(x){return x.unlocked;}).length;
    var level=Math.floor(unlocked/2)+1;
    var levelLabel=level<=1?'🥉 Tân binh':level<=3?'🥈 Độc giả':level<=5?'🥇 Chuyên gia':'💎 Bậc thầy';
    return'<div class=""page"">'+
      '<div class=""hero"" style=""background:linear-gradient(120deg,#7e22ce,#c026d3,#FDE68A);""><h2>🏆 Thành tích của bạn</h2>'+
      '<p>Cấp độ: <b>'+levelLabel+' (Lv.'+level+')</b> • Đã mở khóa: <b>'+unlocked+'/'+a.length+'</b> huy hiệu</p></div>'+
      '<div class=""achievement-grid"">'+
      a.map(function(x){
        var pct=Math.round(x.progress*100/x.target);
        return'<div class=""achv-card '+(x.unlocked?'unlocked':'')+'"">'+
          '<div class=""achv-icon"">'+x.icon+'</div>'+
          '<div class=""achv-title"">'+esc(x.title)+'</div>'+
          '<div class=""achv-desc"">'+esc(x.description)+'</div>'+
          '<div style=""font-size:11px;color:var(--muted);margin-top:6px;"">'+x.progress+'/'+x.target+' ('+pct+'%)</div>'+
          '<div class=""progress-bar""><div class=""progress-fill"" style=""width:'+pct+'%""></div></div>'+
        '</div>';
      }).join('')+
      '</div></div>';
  }
  function pgAnalytics(){
    var s=window.appState,a=s.analytics||{};
    return'<div class=""page"">'+
      '<div style=""display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px;""><div class=""section-title"" style=""margin:0;"">📊 Live Analytics Dashboard</div>'+
      '<button class=""btn btn-yellow"" onclick=""window.doExportAnalytics()"">📄 Xuất báo cáo PDF</button></div>'+
      '<div class=""stat-cards"" style=""margin-top:14px;"">'+
        '<div class=""stat-card""><div class=""stat-number"">'+(s.orders||[]).length+'</div><div class=""stat-label"">Tổng đơn</div></div>'+
        '<div class=""stat-card yellow""><div class=""stat-number"">'+money((s.orders||[]).reduce(function(x,o){return x+o.totalAmount;},0))+'</div><div class=""stat-label"">Tổng doanh thu</div></div>'+
        '<div class=""stat-card green""><div class=""stat-number"">'+(s.books||[]).length+'</div><div class=""stat-label"">Đầu sách</div></div>'+
        '<div class=""stat-card red""><div class=""stat-number"">'+(s.users||[]).length+'</div><div class=""stat-label"">Người dùng</div></div>'+
      '</div>'+
      '<div class=""chart-grid"">'+
        '<div class=""chart-card""><h3>💰 Doanh thu 6 tháng</h3><canvas id=""chart_rev""></canvas></div>'+
        '<div class=""chart-card""><h3>📚 Top 5 sách bán chạy</h3><canvas id=""chart_top""></canvas></div>'+
        '<div class=""chart-card""><h3>🛒 Mượn vs Mua</h3><canvas id=""chart_mode""></canvas></div>'+
        '<div class=""chart-card""><h3>📂 Phân bố danh mục</h3><canvas id=""chart_cat""></canvas></div>'+
      '</div></div>';
  }
  function pgNotifications(){
    var s=window.appState,items=s.notifications||[];
    return'<div class=""page""><div class=""section-title"">🔔 Tất cả thông báo</div>'+
      (items.length?'<div style=""background:var(--card);border-radius:10px;box-shadow:var(--shadow);overflow:hidden;"">'+
      items.map(function(n){
        var d=new Date(n.sentAt);
        return'<div class=""notif-item notif-kind-'+(n.kind||'info')+(n.isRead?'':' unread')+'"" onclick=""window.doMarkRead('+n.id+')"">'+esc(n.message)+'<div class=""meta"">'+d.toLocaleString('vi-VN')+'</div></div>';
      }).join('')+'</div>':'<p style=""color:var(--muted);"">Chưa có thông báo nào.</p>')+
    '</div>';
  }
  window.renderCharts=function(){
    if(typeof Chart==='undefined')return;
    var a=(window.appState||{}).analytics||{};
    var isDark=document.body.classList.contains('dark');
    var txt=isDark?'#E2E8F0':'#333';
    var grid=isDark?'rgba(255,255,255,0.08)':'rgba(0,0,0,0.08)';
    Chart.defaults.color=txt;Chart.defaults.borderColor=grid;
    function mk(id,cfg){var c=document.getElementById(id);if(!c)return;if(c.__chart)c.__chart.destroy();c.__chart=new Chart(c.getContext('2d'),cfg);}
    mk('chart_rev',{type:'line',data:{labels:(a.monthsRev||[]).map(function(x){return x.label;}),datasets:[{label:'Doanh thu (đ)',data:(a.monthsRev||[]).map(function(x){return x.revenue;}),borderColor:'#1E88E5',backgroundColor:'rgba(30,136,229,.2)',fill:true,tension:.3}]},options:{responsive:true,maintainAspectRatio:false,plugins:{legend:{display:false}}}});
    mk('chart_top',{type:'bar',data:{labels:(a.topBooks||[]).map(function(x){return x.name.length>18?x.name.substr(0,16)+'…':x.name;}),datasets:[{label:'Đã bán',data:(a.topBooks||[]).map(function(x){return x.sold;}),backgroundColor:'#FBBF24'},{label:'Lượt mượn',data:(a.topBooks||[]).map(function(x){return x.borrow;}),backgroundColor:'#1E88E5'}]},options:{responsive:true,maintainAspectRatio:false,indexAxis:'y'}});
    mk('chart_mode',{type:'doughnut',data:{labels:['Mượn','Mua'],datasets:[{data:[a.borrowCount||0,a.buyCount||0],backgroundColor:['#1E88E5','#FBBF24']}]},options:{responsive:true,maintainAspectRatio:false}});
    mk('chart_cat',{type:'pie',data:{labels:(a.byCategory||[]).map(function(x){return x.category;}),datasets:[{data:(a.byCategory||[]).map(function(x){return x.sold;}),backgroundColor:['#1E88E5','#FBBF24','#10B981','#EF4444','#8B5CF6','#F59E0B','#06B6D4']}]},options:{responsive:true,maintainAspectRatio:false}});
  };
  window.__editBookId=null;
  window.openBookForm=function(id){window.__editBookId=id||null;var m=document.getElementById('bookModal');if(m)m.classList.add('show');window.__fillBookForm();};
  window.closeBookForm=function(){window.__editBookId=null;var m=document.getElementById('bookModal');if(m)m.classList.remove('show');};
  window.__fillBookForm=function(){
    var s=window.appState,b=null;
    if(window.__editBookId){b=(s.books||[]).find(function(x){return x.id===window.__editBookId;});}
    var defaults={name:'',author:'',category:'',summary:'',coverImageUrl:'',basePrice:0,inventoryCount:0,discountPercent:5,numberPage:0,datePublication:''};
    var d=b||defaults;
    ['name','author','category','summary','coverImageUrl','basePrice','inventoryCount','discountPercent','numberPage','datePublication'].forEach(function(k){
      var el=document.getElementById('bf_'+k);if(el)el.value=d[k]!=null?d[k]:'';
    });
    var t=document.getElementById('bookFormTitle');if(t)t.textContent=b?('Sửa sách #'+b.id):'Thêm sách mới';
  };
  window.submitBookForm=function(){
    var payload={
      action:window.__editBookId?'updateBook':'createBook',
      id:window.__editBookId,
      name:(document.getElementById('bf_name')||{}).value||'',
      author:(document.getElementById('bf_author')||{}).value||'',
      category:(document.getElementById('bf_category')||{}).value||'',
      summary:(document.getElementById('bf_summary')||{}).value||'',
      coverImageUrl:(document.getElementById('bf_coverImageUrl')||{}).value||'',
      basePrice:Number((document.getElementById('bf_basePrice')||{}).value||0),
      inventoryCount:parseInt((document.getElementById('bf_inventoryCount')||{}).value||'0'),
      discountPercent:parseInt((document.getElementById('bf_discountPercent')||{}).value||'5'),
      numberPage:parseInt((document.getElementById('bf_numberPage')||{}).value||'0'),
      datePublication:(document.getElementById('bf_datePublication')||{}).value||''
    };
    if(!payload.name.trim()){alert('Tên sách không được trống');return;}
    window.closeBookForm();
    window.callHost(payload);
  };
  window.deleteBook=function(id,name){if(confirm('Xoá sách ""'+name+'""?\\n(Không thể xoá sách đã có trong đơn.)'))window.callHost({action:'deleteBook',id:id});};
  function bookModalHtml(){
    return'<div id=""bookModal"" class=""modal-backdrop"" onclick=""if(event.target===this)window.closeBookForm()"">'+
      '<div class=""modal-card"">'+
        '<div style=""display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;"">'+
          '<h3 id=""bookFormTitle"" style=""color:var(--primary-dark);"">Thêm sách mới</h3>'+
          '<button class=""icon-btn"" style=""background:#eee;color:#333;"" onclick=""window.closeBookForm()"">✕</button>'+
        '</div>'+
        '<div style=""display:grid;grid-template-columns:1fr 1fr;gap:10px;"">'+
          '<div class=""form-group""><label>Tên sách *</label><input id=""bf_name""></div>'+
          '<div class=""form-group""><label>Tác giả</label><input id=""bf_author""></div>'+
          '<div class=""form-group""><label>Danh mục</label><input id=""bf_category"" placeholder=""IT, Van hoc, Kinh te...""></div>'+
          '<div class=""form-group""><label>Ảnh bìa (URL)</label><input id=""bf_coverImageUrl"" placeholder=""images/books/img-01.jpg""></div>'+
          '<div class=""form-group""><label>Giá cơ bản (đ)</label><input id=""bf_basePrice"" type=""number"" min=""0""></div>'+
          '<div class=""form-group""><label>Tồn kho</label><input id=""bf_inventoryCount"" type=""number"" min=""0""></div>'+
          '<div class=""form-group""><label>Giảm giá % (5-10)</label><input id=""bf_discountPercent"" type=""number"" min=""0"" max=""50""></div>'+
          '<div class=""form-group""><label>Số trang</label><input id=""bf_numberPage"" type=""number"" min=""0""></div>'+
          '<div class=""form-group"" style=""grid-column:1/-1;""><label>Ngày xuất bản</label><input id=""bf_datePublication"" placeholder=""YYYY-MM-DD""></div>'+
          '<div class=""form-group"" style=""grid-column:1/-1;""><label>Tóm tắt</label><textarea id=""bf_summary"" rows=""3""></textarea></div>'+
        '</div>'+
        '<div style=""display:flex;gap:8px;justify-content:flex-end;margin-top:14px;"">'+
          '<button class=""btn btn-yellow"" onclick=""window.closeBookForm()"">Huỷ</button>'+
          '<button class=""btn btn-primary"" onclick=""window.submitBookForm()"">Lưu</button>'+
        '</div>'+
      '</div></div>';
  }
  function pgBooks(){
    var s=window.appState,canEdit=s.role==='Admin'||s.role==='Librarian';
    return'<div class=""page"">'+
      '<div style=""display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px;"">'+
        '<div class=""section-title"" style=""margin:0;"">📚 Quản lý sách &amp; Tồn kho</div>'+
        (canEdit?'<button class=""btn btn-primary"" onclick=""window.openBookForm(null)"">➕ Thêm sách</button>':'')+
      '</div>'+
      '<table style=""margin-top:14px;""><thead><tr><th>Ảnh</th><th>Tên sách</th><th>Tác giả</th><th>Danh mục</th><th>Giá</th><th>Tồn kho</th><th>Mượn</th><th>Bán</th>'+(canEdit?'<th>Hành động</th>':'')+'</tr></thead><tbody>'+
      (s.books||[]).map(function(b){return'<tr><td><img src=""'+esc(b.coverImageUrl)+'"" onerror=""this.src=\'images/books/img-01.jpg\'"" style=""width:52px;height:52px;object-fit:cover;border-radius:6px;""/></td><td><b>'+esc(b.name)+'</b></td><td>'+esc(b.author)+'</td><td>'+esc(b.category)+'</td><td>'+money(b.basePrice)+'</td><td><b>'+b.inventoryCount+'</b></td><td>'+b.borrowCount+'</td><td>'+b.numberSold+'</td>'+(canEdit?'<td style=""white-space:nowrap;""><button class=""btn btn-yellow btn-sm"" onclick=""window.openBookForm('+b.id+')"">✏️ Sửa</button> <button class=""btn btn-red btn-sm"" onclick=""window.deleteBook('+b.id+',\''+esc(b.name).replace(/\x27/g,'')+'\')"">🗑️</button></td>':'')+'</tr>';}).join('')+
      '</tbody></table>'+(canEdit?bookModalHtml():'')+'</div>';
  }

  window.renderApp=function(){
    var s=window.appState||{view:'login'},v=s.view||'login',html='';
    window.__currentView=v;
    switch(v){
      case'login':html=pgLogin();break;case'signup':html=pgSignup();break;
      case'home':html=pgHome();break;case'products':html=pgProducts();break;
      case'detail':html=pgDetail();break;case'cart':html=pgCart();break;
      case'orders':html=pgOrders();break;case'profile':html=pgProfile();break;
      case'librarian':html=pgLibrarian();break;case'admin':html=pgAdmin();break;
      case'books':html=pgBooks();break;case'users':html=pgAdmin();break;
      case'analytics':html=pgAnalytics();break;
      case'achievements':html=pgAchievements();break;
      case'notifications':html=pgNotifications();break;
      default:html=pgHome();
    }
    var wrap=document.getElementById('root');
    if(wrap)wrap.innerHTML=navBar()+html;
    if(v==='analytics'&&window.renderCharts){setTimeout(window.renderCharts,30);}
  };
})();";
}
