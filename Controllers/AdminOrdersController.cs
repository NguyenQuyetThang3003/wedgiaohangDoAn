using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Danh sách tất cả đơn hàng + bộ lọc cơ bản.
        /// URL: /AdminOrders
        /// </summary>
        public async Task<IActionResult> Index(
            string? status,       // pending / shipping / done / failed / null (tất cả)
            string? keyword,      // tìm theo mã đơn / tên / SĐT người nhận
            DateTime? fromDate,   // lọc theo ngày tạo từ ...
            DateTime? toDate,     // ... đến
            int page = 1,
            int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            // ==========================
            // 1) Query gốc
            // ==========================
            var query = _db.Orders
                .Include(o => o.User)            // join để lấy tên khách
                .AsQueryable();

            // ==========================
            // 2) Lọc theo trạng thái
            // ==========================
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                query = query.Where(o => o.Status == status);
            }

            // ==========================
            // 3) Lọc theo keyword
            // ==========================
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

            // ==========================
            // 4) Lọc theo ngày tạo
            // ==========================
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

            // ==========================
            // 5) Sắp xếp & phân trang
            // ==========================
            query = query.OrderByDescending(o => o.CreatedAt);

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new AdminOrderListItemViewModel
                {
                    Id = o.Id,
                    Code = o.Code ?? "",
                    CustomerName = o.User != null ? o.User.UserName ?? "" : "(Khách lẻ)",
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

            // ==========================
            // 6) Thống kê theo trạng thái
            // ==========================
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
    }

    // ==========================
    // VIEW MODELS
    // ==========================

    /// <summary>
    /// Item hiển thị từng dòng trong bảng "Tất cả đơn hàng"
    /// </summary>
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

    /// <summary>
    /// Thống kê số đơn theo trạng thái
    /// </summary>
    public class AdminOrderStatusStat
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
    }

    /// <summary>
    /// ViewModel cho trang Index (list + bộ lọc + phân trang)
    /// </summary>
    public class AdminOrderListViewModel
    {
        public List<AdminOrderListItemViewModel> Items { get; set; } = new();

        // Bộ lọc
        public string? StatusFilter { get; set; }
        public string? Keyword { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Phân trang
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Thống kê trạng thái
        public List<AdminOrderStatusStat> StatusStats { get; set; } = new();
    }
}
