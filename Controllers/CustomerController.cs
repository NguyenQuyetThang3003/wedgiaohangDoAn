using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;

namespace WedNightFury.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        // ===========================
        // DASHBOARD
        // ===========================
        public IActionResult Dashboard(DateTime? fromDate, DateTime? toDate)
        {
            // 1. Kiểm tra đăng nhập
            var username = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("Role");
            var customerId = HttpContext.Session.GetInt32("UserId");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) ||
                role.ToLower() != "customer" || customerId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // 2. Auto khoảng thời gian = tháng hiện tại nếu chưa chọn
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            if (!fromDate.HasValue) fromDate = startOfMonth;
            if (!toDate.HasValue)   toDate = endOfMonth;

            ViewBag.FromDate = fromDate.Value;
            ViewBag.ToDate = toDate.Value;

            // 3. Lọc đơn theo khách hàng + khoảng thời gian
            var orders = _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Where(o => o.CreatedAt.HasValue &&
                            o.CreatedAt.Value.Date >= fromDate.Value.Date &&
                            o.CreatedAt.Value.Date <= toDate.Value.Date)
                .AsQueryable();

            // 4. Tổng quan
            ViewBag.SuccessCount = orders.Count(o => o.Status != null && o.Status.ToLower() == "done");
            ViewBag.FailCount    = orders.Count(o => o.Status != null && o.Status.ToLower() == "cancelled");
            ViewBag.TotalOrders  = orders.Count();

            // 5. BAR CHART – thống kê theo tháng (trong khoảng lọc)
            var rawBarData = orders
                .GroupBy(o => new { o.CreatedAt!.Value.Year, o.CreatedAt.Value.Month })
                .Select(g => new
                {
                    Year    = g.Key.Year,
                    Month   = g.Key.Month,
                    Success = g.Count(x => x.Status != null && x.Status.ToLower() == "done"),
                    Fail    = g.Count(x => x.Status != null && x.Status.ToLower() == "cancelled"),
                    Shipping= g.Count(x => x.Status != null && x.Status.ToLower() == "shipping"),
                    Pending = g.Count(x => x.Status != null && x.Status.ToLower() == "pending")
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();

            ViewBag.BarData = rawBarData.Select(x => new
            {
                month    = $"{x.Month:D2}/{x.Year}",
                success  = x.Success,
                fail     = x.Fail,
                shipping = x.Shipping,
                pending  = x.Pending
            }).ToList();

            // 6. PIE CHART – top 10 theo tỉnh (trong khoảng lọc)
            var pie = orders
                .Where(o => !string.IsNullOrEmpty(o.Province))
                .GroupBy(o => o.Province!)
                .Select(g => new { province = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList();

            ViewBag.PieData = pie.Any()
                ? pie
                : orders.GroupBy(o => o.Status ?? "Không rõ")
                        .Select(g => new { province = g.Key, count = g.Count() })
                        .ToList();

            // 7. Thông tin hiển thị
            ViewBag.CustomerName = username;
            ViewBag.LastUpdate   = DateTime.Now.ToString("HH:mm");

            return View();
        }

        // ===========================
        // EXPORT EXCEL
        // ===========================
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate)
        {
            // 1. Kiểm tra đăng nhập
            var username = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("Role");
            var customerId = HttpContext.Session.GetInt32("UserId");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) ||
                role.ToLower() != "customer" || customerId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // 2. Auto tháng hiện tại nếu không truyền
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            if (!fromDate.HasValue) fromDate = startOfMonth;
            if (!toDate.HasValue)   toDate = endOfMonth;

            // 3. Lọc đơn giống Dashboard
            var orders = _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Where(o => o.CreatedAt.HasValue &&
                            o.CreatedAt.Value.Date >= fromDate.Value.Date &&
                            o.CreatedAt.Value.Date <= toDate.Value.Date)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            // 4. Tạo file Excel
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Orders");

                int row = 1;

                ws.Cell(row, 1).Value = "Mã đơn";
                ws.Cell(row, 2).Value = "Người gửi";
                ws.Cell(row, 3).Value = "Người nhận";
                ws.Cell(row, 4).Value = "Giá trị (VNĐ)";
                ws.Cell(row, 5).Value = "Trạng thái";
                ws.Cell(row, 6).Value = "Tỉnh/Thành phố";
                ws.Cell(row, 7).Value = "Ngày tạo";

                ws.Range(row, 1, row, 7).Style.Font.Bold = true;

                foreach (var o in orders)
                {
                    row++;
                    ws.Cell(row, 1).Value = o.Code;
                    ws.Cell(row, 2).Value = o.SenderName;
                    ws.Cell(row, 3).Value = o.ReceiverName;
                    ws.Cell(row, 4).Value = o.Value; // hoặc CodAmount nếu muốn
                    ws.Cell(row, 5).Value = o.Status;
                    ws.Cell(row, 6).Value = o.Province;
                    ws.Cell(row, 7).Value = o.CreatedAt?.ToString("dd/MM/yyyy HH:mm");
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    var fileName = $"DonHang_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        fileName
                    );
                }
            }
        }
    }
}
