using LibraryManagement.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LibraryManagement.Wpf;

public static class PdfExporter
{
    static PdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GenerateInvoice(BorrowOrder order, AppUser user, IEnumerable<Book> books)
    {
        var blue = Colors.Blue.Darken2;
        var yellow = Colors.Yellow.Lighten3;

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(30);
                p.DefaultTextStyle(s => s.FontSize(11).FontFamily("Segoe UI"));

                p.Header().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("LIBRARY MANAGEMENT").Bold().FontSize(20).FontColor(blue);
                        c.Item().Text("Hệ thống quản lý thư viện").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    r.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().Background(yellow).Padding(8).Text($"HÓA ĐƠN #{order.Id:D5}").Bold().FontSize(14).FontColor(blue);
                        c.Item().PaddingTop(4).Text($"Ngày: {order.BorrowDate:dd/MM/yyyy}");
                    });
                });

                p.Content().PaddingVertical(14).Column(c =>
                {
                    c.Item().Background(Colors.Blue.Lighten5).Padding(10).Column(cc =>
                    {
                        cc.Item().Text("THÔNG TIN KHÁCH HÀNG").Bold().FontColor(blue);
                        cc.Item().Text($"Họ tên: {user.FullName}");
                        cc.Item().Text($"Email: {user.Email}");
                        cc.Item().Text($"SĐT: {user.Phone}   Địa chỉ: {user.Address}");
                    });

                    c.Item().PaddingTop(12).Text("CHI TIẾT ĐƠN HÀNG").Bold().FontColor(blue);

                    c.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(30);
                            cd.RelativeColumn(4);
                            cd.ConstantColumn(60);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(90);
                            cd.ConstantColumn(90);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Background(blue).Padding(5).Text("#").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Sách").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Loại").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("SL").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).AlignRight().Text("Đơn giá").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).AlignRight().Text("Thành tiền").FontColor(Colors.White).Bold();
                        });

                        var idx = 1;
                        foreach (var i in order.Items)
                        {
                            var bookName = books.FirstOrDefault(b => b.Id == i.BookId)?.Name ?? $"Book #{i.BookId}";
                            var bg = idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                            t.Cell().Background(bg).Padding(5).Text(idx.ToString());
                            t.Cell().Background(bg).Padding(5).Text(bookName);
                            t.Cell().Background(bg).Padding(5).Text(i.IsPurchase ? "Mua" : "Mượn");
                            t.Cell().Background(bg).Padding(5).Text(i.Quantity.ToString());
                            t.Cell().Background(bg).Padding(5).AlignRight().Text($"{i.UnitPrice:N0}đ");
                            t.Cell().Background(bg).Padding(5).AlignRight().Text($"{i.UnitPrice * i.Quantity:N0}đ");
                            idx++;
                        }
                    });

                    c.Item().PaddingTop(10).AlignRight().Column(cc =>
                    {
                        cc.Item().Text($"Tổng cộng: {order.TotalAmount:N0}đ").Bold().FontSize(13).FontColor(blue);
                        if (order.LateFeeAmount > 0)
                        {
                            cc.Item().Text($"Phí trễ hạn: {order.LateFeeAmount:N0}đ").FontColor(Colors.Red.Medium);
                        }
                        cc.Item().Text($"Hạn trả: {order.DueDate:dd/MM/yyyy}");
                        cc.Item().Text($"Trạng thái: {order.Status}").Italic();
                    });
                });

                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Library Management © 2026  •  Trang ").FontSize(9).FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    t.Span("/").FontSize(9).FontColor(Colors.Grey.Medium);
                    t.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateAnalyticsReport(IEnumerable<Book> books, IEnumerable<BorrowOrder> orders, IEnumerable<AppUser> users)
    {
        var blue = Colors.Blue.Darken2;
        var totalRev = orders.Sum(o => o.TotalAmount);
        var totalOrders = orders.Count();
        var topBooks = books.OrderByDescending(b => b.NumberSold).Take(10).ToList();

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(30);
                p.DefaultTextStyle(s => s.FontSize(11).FontFamily("Segoe UI"));

                p.Header().Column(c =>
                {
                    c.Item().Text("BÁO CÁO THỐNG KÊ HỆ THỐNG").Bold().FontSize(20).FontColor(blue);
                    c.Item().Text($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                p.Content().PaddingVertical(14).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Background(Colors.Blue.Lighten5).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Tổng đơn").FontSize(11).FontColor(Colors.Grey.Darken1);
                            cc.Item().Text(totalOrders.ToString()).Bold().FontSize(22).FontColor(blue);
                        });
                        r.ConstantItem(10);
                        r.RelativeItem().Background(Colors.Yellow.Lighten4).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Doanh thu").FontSize(11).FontColor(Colors.Grey.Darken1);
                            cc.Item().Text($"{totalRev:N0}đ").Bold().FontSize(22).FontColor(blue);
                        });
                        r.ConstantItem(10);
                        r.RelativeItem().Background(Colors.Green.Lighten4).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Tổng sách").FontSize(11).FontColor(Colors.Grey.Darken1);
                            cc.Item().Text(books.Count().ToString()).Bold().FontSize(22).FontColor(blue);
                        });
                        r.ConstantItem(10);
                        r.RelativeItem().Background(Colors.Orange.Lighten4).Padding(10).Column(cc =>
                        {
                            cc.Item().Text("Users").FontSize(11).FontColor(Colors.Grey.Darken1);
                            cc.Item().Text(users.Count().ToString()).Bold().FontSize(22).FontColor(blue);
                        });
                    });

                    c.Item().PaddingTop(14).Text("TOP 10 SÁCH BÁN CHẠY").Bold().FontColor(blue);
                    c.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(30);
                            cd.RelativeColumn(4);
                            cd.RelativeColumn(2);
                            cd.ConstantColumn(60);
                            cd.ConstantColumn(60);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Background(blue).Padding(5).Text("#").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Tên sách").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Tác giả").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Bán").FontColor(Colors.White).Bold();
                            h.Cell().Background(blue).Padding(5).Text("Mượn").FontColor(Colors.White).Bold();
                        });
                        var idx = 1;
                        foreach (var b in topBooks)
                        {
                            var bg = idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                            t.Cell().Background(bg).Padding(5).Text(idx.ToString());
                            t.Cell().Background(bg).Padding(5).Text(b.Name);
                            t.Cell().Background(bg).Padding(5).Text(b.Author);
                            t.Cell().Background(bg).Padding(5).Text(b.NumberSold.ToString());
                            t.Cell().Background(bg).Padding(5).Text(b.BorrowCount.ToString());
                            idx++;
                        }
                    });
                });

                p.Footer().AlignCenter().Text("Library Management © 2026").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
}
