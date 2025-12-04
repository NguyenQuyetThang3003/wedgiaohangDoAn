using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;

namespace WedNightFury.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET /Order/Create – KHÁCH TẠO ĐƠN
        // =========================================================
        public IActionResult Create()
        {
            // 🔒 Kiểm tra đăng nhập
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var model = new Order();

            // 🔄 Lấy profile để tự điền NGƯỜI GỬI + city
            var profile = _context.Profiles.FirstOrDefault(p => p.UserId == userId.Value);
            if (profile != null)
            {
                model.SenderName    = profile.FullName;
                model.SenderPhone   = profile.Phone;
                model.SenderAddress = profile.Address;

                // City của người gửi – dùng để check nội/ngoại thành bên JS
                ViewBag.SenderCity = profile.City;
            }
            else
            {
                ViewBag.SenderCity = "";
            }

            return View(model);
        }

        // =========================================================
        // POST /Order/Create – LƯU ĐƠN
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Order model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // --- Giá trị cấu hình lấy từ form (radio/select) ---
            model.GoodsType    = Request.Form["GoodsType"];
            model.AreaType     = Request.Form["AreaType"];      // inner / outer
            model.PickupMethod = Request.Form["PickupMethod"];  // pickup / hub
            model.ServiceLevel = Request.Form["ServiceLevel"];  // standard / fast / express
            model.ShipPayer    = Request.Form["ShipPayer"];     // sender / receiver

            // --- Làm sạch các giá trị tiền (lấy từ hidden input) ---
            model.Value     = ParseDecimal(Request.Form["Value"]);
            model.CodAmount = ParseDecimal(Request.Form["CodAmount"]);
            model.ShipFee   = ParseDecimal(Request.Form["ShipFee"]);

            // --- Ghép địa chỉ đầy đủ người nhận ---
            string province = Request.Form["Province"];
            string district = Request.Form["District"];
            string ward     = Request.Form["Ward"];

            string detail = model.ReceiverAddress ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(ward) ||
                !string.IsNullOrWhiteSpace(district) ||
                !string.IsNullOrWhiteSpace(province))
            {
                if (!string.IsNullOrWhiteSpace(detail))
                    model.ReceiverAddress = $"{detail}, {ward}, {district}, {province}".Trim().Trim(',');
                else
                    model.ReceiverAddress = $"{ward}, {district}, {province}".Trim().Trim(',');
            }

            // Lưu luôn tỉnh để sau này thống kê / kiểm tra nội-ngoại thành
            model.Province = province;

            // --- Thông tin hệ thống cho đơn hàng ---
            model.Code       = $"NF-{DateTime.Now:yyyyMMddHHmmss}";
            model.Status     = "pending";
            model.CreatedAt  = DateTime.Now;
            model.CustomerId = userId.Value;

            _context.Orders.Add(model);
            _context.SaveChanges();

            TempData["OrderId"]   = model.Id;
            TempData["OrderCode"] = model.Code;

            return RedirectToAction("Success");
        }

        // Hàm phụ parse decimal từ string (có thể có . , ngăn cách)
        private decimal ParseDecimal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            raw = raw.Replace(".", "").Replace(",", "");
            return decimal.TryParse(raw, out var v) ? v : 0;
        }

        // =========================================================
        // /Order/Success – THÔNG BÁO SAU KHI TẠO ĐƠN
        // =========================================================
        public IActionResult Success()
        {
            ViewBag.OrderId   = TempData["OrderId"];
            ViewBag.OrderCode = TempData["OrderCode"];
            return View();
        }

        // =========================================================
        // /Order/Manage – QUẢN LÝ VẬN ĐƠN (KHÁCH / ADMIN)
        // =========================================================
        public IActionResult Manage(string? status, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders.AsQueryable();

            // Lọc theo trạng thái (chung với bên tài xế)
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "cancelled")
                {
                    // "Đã hủy" hiển thị cả cancelled + failed
                    q = q.Where(o => o.Status == "cancelled" || o.Status == "failed");
                }
                else
                {
                    q = q.Where(o => o.Status == status);
                }
            }

            // Lọc ngày tạo
            if (startDate.HasValue)
            {
                var from = startDate.Value.Date;
                q = q.Where(o => o.CreatedAt >= from);
            }

            if (endDate.HasValue)
            {
                // < endDate + 1 day để không miss giờ trong ngày đó
                var to = endDate.Value.Date.AddDays(1);
                q = q.Where(o => o.CreatedAt < to);
            }

            // Thống kê theo bộ lọc hiện tại
            ViewBag.TotalOrders     = q.Count();
            ViewBag.PendingOrders   = q.Count(o => o.Status == "pending");
            ViewBag.ShippingOrders  = q.Count(o => o.Status == "shipping");
            ViewBag.DoneOrders      = q.Count(o => o.Status == "done");
            ViewBag.CancelledOrders = q.Count(o => o.Status == "cancelled" || o.Status == "failed");

            var list = q
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            return View(list);
        }

        // =========================================================
        // /Order/Pending – ĐƠN CẦN XỬ LÝ (pending + shipping)
        // =========================================================
        public IActionResult Pending(DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders
                .Where(o => o.Status == "pending" || o.Status == "shipping");

            if (startDate.HasValue)
            {
                var from = startDate.Value.Date;
                q = q.Where(o => o.CreatedAt >= from);
            }

            if (endDate.HasValue)
            {
                var to = endDate.Value.Date.AddDays(1);
                q = q.Where(o => o.CreatedAt < to);
            }

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate   = endDate?.ToString("yyyy-MM-dd");

            return View(q.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // =========================================================
        // /Order/Details/{id} – CHI TIẾT ĐƠN
        // =========================================================
        public IActionResult Details(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }

        // =========================================================
        // POST /Order/UpdateStatus – ĐỔI TRẠNG THÁI TỪ MÀN QUẢN LÝ
        // (dropdown "Trạng thái" ở view Manage)
        // =========================================================
        [HttpPost]
        public IActionResult UpdateStatus(int id, string newStatus)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            order.Status = newStatus;

            // Nếu cập nhật sang Hoàn tất / Hủy thì set mốc thời gian nếu chưa có
            if (newStatus == "done" && !order.DeliveredAt.HasValue)
            {
                order.DeliveredAt = DateTime.Now;
            }

            if ((newStatus == "cancelled" || newStatus == "failed") && !order.FailedAt.HasValue)
            {
                order.FailedAt = DateTime.Now;
            }

            _context.SaveChanges();

            TempData["Message"] = "Đã cập nhật trạng thái đơn.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
