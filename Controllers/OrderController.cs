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

        // =========================
        // GET /Order/Create
        // =========================
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

        // =========================
        // POST /Order/Create
        // =========================
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

            // ⚠ Bỏ qua ModelState.IsValid cho đơn hàng (tránh bị kẹt do decimal/culture)
            // Nếu muốn validate sau này, có thể thêm kiểm tra riêng.

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

        // =========================
        // /Order/Success
        // =========================
        public IActionResult Success()
        {
            ViewBag.OrderId   = TempData["OrderId"];
            ViewBag.OrderCode = TempData["OrderCode"];
            return View();
        }

        // =========================
        // DANH SÁCH ĐƠN (dùng cho admin)
        // =========================
        public IActionResult Manage(string? status, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
                q = q.Where(o => o.Status == status);

            if (startDate.HasValue)
                q = q.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                q = q.Where(o => o.CreatedAt <= endDate.Value);

            ViewBag.TotalOrders     = _context.Orders.Count();
            ViewBag.PendingOrders   = _context.Orders.Count(o => o.Status == "pending");
            ViewBag.ShippingOrders  = _context.Orders.Count(o => o.Status == "shipping");
            ViewBag.DoneOrders      = _context.Orders.Count(o => o.Status == "done");
            ViewBag.CancelledOrders = _context.Orders.Count(o => o.Status == "cancelled");

            return View(q.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // =========================
// ĐƠN CẦN XỬ LÝ (pending + shipping)
        // =========================
        public IActionResult Pending(DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders
                .Where(o => o.Status == "pending" || o.Status == "shipping");

            if (startDate.HasValue)
                q = q.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                q = q.Where(o => o.CreatedAt <= endDate.Value);

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate   = endDate?.ToString("yyyy-MM-dd");

            return View(q.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // =========================
        // Chi tiết đơn
        // =========================
        public IActionResult Details(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }
    }
}