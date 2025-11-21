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

        // GET /Order/Create
        public IActionResult Create() => View();

        // POST /Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Order model)
        {
            // Clean tiền
            model.Value = decimal.TryParse(Request.Form["Value"]
                .ToString().Replace(".", "").Replace(",", ""), out var v) ? v : 0;

            model.CodAmount = decimal.TryParse(Request.Form["CodAmount"]
                .ToString().Replace(".", "").Replace(",", ""), out var cod) ? cod : 0;

            model.ShipFee = decimal.TryParse(Request.Form["ShipFee"]
                .ToString().Replace(".", "").Replace(",", ""), out var fee) ? fee : 0;

            // Ghép địa chỉ đầy đủ
            string province = Request.Form["Province"];
            string district = Request.Form["District"];
            string ward = Request.Form["Ward"];

            string detail = model.ReceiverAddress ?? "";

            model.ReceiverAddress = $"{detail}, {ward}, {district}, {province}";

            model.Province = province;

            // System info
            model.Code = $"NF-{DateTime.Now:yyyyMMddHHmmss}";
            model.Status = "pending";
            model.CreatedAt = DateTime.Now;
            model.CustomerId = 1;

            _context.Orders.Add(model);
            _context.SaveChanges();

            TempData["OrderId"] = model.Id;
            TempData["OrderCode"] = model.Code;

            return RedirectToAction("Success");
        }

        public IActionResult Success()
        {
            ViewBag.OrderId = TempData["OrderId"];
            ViewBag.OrderCode = TempData["OrderCode"];
            return View();
        }

        // DANH SÁCH ĐƠN
        public IActionResult Manage(string? status, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
                q = q.Where(o => o.Status == status);

            if (startDate.HasValue)
                q = q.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                q = q.Where(o => o.CreatedAt <= endDate.Value);

            ViewBag.TotalOrders = _context.Orders.Count();
            ViewBag.PendingOrders = _context.Orders.Count(o => o.Status == "pending");
            ViewBag.ShippingOrders = _context.Orders.Count(o => o.Status == "shipping");
            ViewBag.DoneOrders = _context.Orders.Count(o => o.Status == "done");
            ViewBag.CancelledOrders = _context.Orders.Count(o => o.Status == "cancelled");

            return View(q.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // ĐƠN CẦN XỬ LÝ
        public IActionResult Pending(DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders
                .Where(o => o.Status == "pending" || o.Status == "shipping");

            if (startDate.HasValue)
                q = q.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                q = q.Where(o => o.CreatedAt <= endDate.Value);

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(q.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // Chi tiết đơn
        public IActionResult Details(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }
    }
}
