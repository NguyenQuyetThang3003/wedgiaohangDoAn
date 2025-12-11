using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using WedNightFury.Models;
using System;
using System.Linq;

namespace WedNightFury.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard(DateTime? fromDate, DateTime? toDate)
        {
            // 🔒 Kiểm tra đăng nhập
            var username = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("Role");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) ||
                role.ToLower() != "customer")
            {
                return RedirectToAction("Login", "Auth");
            }

            // 🗓️ Auto set khoảng thời gian = tháng hiện tại nếu chưa chọn
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            if (!fromDate.HasValue) fromDate = startOfMonth;
            if (!toDate.HasValue) toDate = endOfMonth;

            // Lưu cho View hiển thị lại trong input date
            ViewBag.FromDate = fromDate.Value;
            ViewBag.ToDate = toDate.Value;

            // 🔍 Lọc đơn hàng theo CreatedAt + khoảng thời gian
            var orders = _context.Orders
                .Where(o => o.CreatedAt.HasValue)
                .Where(o => o.CreatedAt!.Value.Date >= fromDate.Value.Date
                         && o.CreatedAt!.Value.Date <= toDate.Value.Date)
                .AsQueryable();

            // ✅ Tổng quan
            ViewBag.SuccessCount = orders.Count(o => o.Status != null && o.Status.ToLower() == "done");
            ViewBag.FailCount    = orders.Count(o => o.Status != null && o.Status.ToLower() == "cancelled");
            ViewBag.TotalOrders  = orders.Count();

            // ✅ Biểu đồ cột theo tháng/năm (trong khoảng lọc)
            var rawBarData = orders
                .GroupBy(o => new { o.CreatedAt!.Value.Year, o.CreatedAt.Value.Month })
                .Select(g => new
                {
                    Year     = g.Key.Year,
                    Month    = g.Key.Month,
                    Success  = g.Count(x => x.Status != null && x.Status.ToLower() == "done"),
                    Fail     = g.Count(x => x.Status != null && x.Status.ToLower() == "cancelled"),
                    Shipping = g.Count(x => x.Status != null && x.Status.ToLower() == "shipping"),
                    Pending  = g.Count(x => x.Status != null && x.Status.ToLower() == "pending")
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();

            // Dữ liệu gửi sang JS: dùng camelCase
            ViewBag.BarData = rawBarData
                .Select(x => new
                {
                    month    = $"{x.Month:D2}/{x.Year}", // label
                    success  = x.Success,
                    fail     = x.Fail,
                    shipping = x.Shipping,
                    pending  = x.Pending
                })
                .ToList();

            // ✅ Biểu đồ tròn: Top 10 theo tỉnh (trong khoảng lọc)
            var pieByProvince = orders
                .Where(o => !string.IsNullOrEmpty(o.Province))
                .GroupBy(o => o.Province!)
                .Select(g => new { province = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .Take(10)
                .ToList();

            if (pieByProvince.Any())
            {
                ViewBag.PieData = pieByProvince;
            }
            else
            {
                // fallback: group theo trạng thái
                ViewBag.PieData = orders
                    .GroupBy(o => o.Status ?? "Không rõ")
                    .Select(g => new { province = g.Key, count = g.Count() })
                    .OrderByDescending(g => g.count)
                    .ToList();
            }

            // 👤 Thông tin chung
            ViewBag.CustomerName = username;
            ViewBag.LastUpdate   = DateTime.Now.ToString("HH:mm");

            return View();
        }
    }
}
