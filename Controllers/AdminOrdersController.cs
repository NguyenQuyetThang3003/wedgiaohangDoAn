using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WedNightFury.Filters;
using WedNightFury.Models;

namespace WedNightFury.Controllers
{
    [AdminAuthorize]
    public class AdminOrdersController : Controller
    {
        private readonly AppDbContext _db;

        public AdminOrdersController(AppDbContext db)
        {
            _db = db;
        }

        // ===========================================================
        // 1. INDEX – TẤT CẢ ĐƠN HÀNG
        // ===========================================================
        public async Task<IActionResult> Index(
            string? status,
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.Orders
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(o =>
                    (o.Code != null && o.Code.Contains(keyword)) ||
                    (o.SenderName != null && o.SenderName.Contains(keyword)) ||
                    (o.ReceiverName != null && o.ReceiverName.Contains(keyword)) ||
                    (o.SenderPhone != null && o.SenderPhone.Contains(keyword)) ||
                    (o.ReceiverPhone != null && o.ReceiverPhone.Contains(keyword))
                );
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(o =>
                    o.CreatedAt.HasValue &&
                    o.CreatedAt.Value.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date;
                query = query.Where(o =>
                    o.CreatedAt.HasValue &&
                    o.CreatedAt.Value.Date <= to);
            }

            query = query.OrderByDescending(o => o.CreatedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new AdminOrderListItemViewModel
                {
                    Id = o.Id,
                    Code = o.Code ?? "",
                    // ✅ SỬA TOÁN TỬ 3 NGÔI
                    CustomerName = o.User != null
                        ? (string.IsNullOrEmpty(o.User.UserName) ? "(Khách lẻ)" : o.User.UserName)
                        : "(Khách lẻ)",
                    ReceiverName = o.ReceiverName ?? "",
                    ReceiverPhone = o.ReceiverPhone ?? "",
                    ReceiverAddress = o.ReceiverAddress ?? "",
                    Province = o.Province ?? "",
                    ProductName = o.ProductName ?? "",
                    CodAmount = o.CodAmount,
                    ShipFee = o.ShipFee,
                    Status = o.Status ?? "",
                    CreatedAt = o.CreatedAt
                })
                .AsNoTracking()
                .ToListAsync();

            var statusStats = await _db.Orders
                .GroupBy(o => o.Status ?? "unknown")
                .Select(g => new AdminOrderStatusStat
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .AsNoTracking()
                .ToListAsync();

            var vm = new AdminOrderListViewModel
            {
                Items = items,
                TotalItems = totalItems,
                PageIndex = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                StatusFilter = status,
                Keyword = keyword,
                FromDate = fromDate,
                ToDate = toDate,
                StatusStats = statusStats
            };

            return View(vm);
        }

        // ===========================================================
        // 2. ĐƠN MỚI / CHỜ PHÂN CÔNG
        // ===========================================================
        public async Task<IActionResult> NewOrders(
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1,
            int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.Orders
                .Include(o => o.User)
                .Where(o => o.DriverId == null &&
                            (o.Status == "pending" ||
                             o.Status == "awaiting_assignment"))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(o =>
                    (o.Code != null && o.Code.Contains(keyword)) ||
                    (o.SenderName != null && o.SenderName.Contains(keyword)) ||
                    (o.ReceiverName != null && o.ReceiverName.Contains(keyword)) ||
                    (o.SenderPhone != null && o.SenderPhone.Contains(keyword)) ||
                    (o.ReceiverPhone != null && o.ReceiverPhone.Contains(keyword))
                );
            }

            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt <= toDate.Value.Date);

            query = query.OrderBy(o => o.CreatedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new AdminOrderListItemViewModel
                {
                    Id = o.Id,
                    Code = o.Code ?? "",
                    // ✅ SỬA TOÁN TỬ 3 NGÔI
                    CustomerName = o.User != null
                        ? (string.IsNullOrEmpty(o.User.UserName) ? "(Khách lẻ)" : o.User.UserName)
                        : "(Khách lẻ)",
                    ReceiverName = o.ReceiverName ?? "",
                    ReceiverPhone = o.ReceiverPhone ?? "",
                    ReceiverAddress = o.ReceiverAddress ?? "",
                    Province = o.Province ?? "",
                    ProductName = o.ProductName ?? "",
                    CodAmount = o.CodAmount,
                    ShipFee = o.ShipFee,
                    Status = o.Status ?? "",
                    CreatedAt = o.CreatedAt
                })
                .AsNoTracking()
                .ToListAsync();

            var vm = new AdminOrderListViewModel
            {
                Items = items,
                TotalItems = totalItems,
                PageIndex = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                Keyword = keyword,
                FromDate = fromDate,
                ToDate = toDate,
                StatusFilter = "new"
            };

            return View(vm);
        }

        // ===========================================================
        // 3. GÁN TÀI XẾ – GET
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> AssignDriver(int id)
        {
            var order = await _db.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            var drivers = await _db.Users
                .Where(u => u.Role == "driver" || u.Role == "taixe")
                .ToListAsync();

            ViewBag.Order = order;
            return View(drivers);
        }

        // ===========================================================
        // 4. GÁN TÀI XẾ – POST
        // ===========================================================
        [HttpPost]
        public async Task<IActionResult> AssignDriver(int orderId, int driverId)
        {
            var order = await _db.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound();

            order.DriverId = driverId;
            order.AssignedAt = DateTime.Now;
            order.DeliveryDate = DateTime.Today;
            order.Status = "assigned";

            await _db.SaveChangesAsync();

            TempData["Success"] = "Gán tài xế thành công!";
            return RedirectToAction(nameof(NewOrders));
        }

        // ===========================================================
        // 5. SEARCH THEO MÃ ĐƠN
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Search(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction(nameof(Index));

            var orders = await _db.Orders
                .Include(o => o.User)
                .Where(o => o.Code != null && o.Code.Contains(code))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var vm = new AdminOrderListViewModel
            {
                Items = orders.Select(o => new AdminOrderListItemViewModel
                {
                    Id = o.Id,
                    Code = o.Code ?? "",
                    CustomerName = o.User?.UserName ?? "(Khách lẻ)",
                    ReceiverName = o.ReceiverName ?? "",
                    ReceiverPhone = o.ReceiverPhone ?? "",
                    ReceiverAddress = o.ReceiverAddress ?? "",
                    Province = o.Province ?? "",
                    ProductName = o.ProductName ?? "",
                    CodAmount = o.CodAmount,
                    ShipFee = o.ShipFee,
                    Status = o.Status ?? "",
                    CreatedAt = o.CreatedAt
                }).ToList()
            };

            return View("Index", vm);
        }

        // ===========================================================
        // 6. API THAY ĐỔI TRẠNG THÁI
        // ===========================================================
        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, string status)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
                return Json(new { ok = false, msg = "Order not found" });

            order.Status = status;
            await _db.SaveChangesAsync();

            return Json(new { ok = true, msg = "Updated" });
        }
    }

    // ========================= VIEW MODELS =========================

    public class AdminOrderListItemViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string ReceiverPhone { get; set; } = "";
        public string ReceiverAddress { get; set; } = "";
        public string Province { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal CodAmount { get; set; }
        public decimal ShipFee { get; set; }
        public string Status { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
    }

    public class AdminOrderStatusStat
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
    }

    public class AdminOrderListViewModel
    {
        public List<AdminOrderListItemViewModel> Items { get; set; } = new();

        public string? StatusFilter { get; set; }
        public string? Keyword { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        public List<AdminOrderStatusStat> StatusStats { get; set; } = new();
    }
}
