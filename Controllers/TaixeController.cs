using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WedNightFury.Models;
using WedNightFury.Models.ViewModels;

namespace WedNightFury.Controllers
{
    [Authorize]
    public class TaixeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TaixeController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? GetCurrentDriverId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        private bool IsDriver()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role == null) return false;

            role = role.ToLower().Trim();
            return role == "driver" || role == "taixe";
        }

        // ==========================================================
        // 📌 DASHBOARD – ĐƠN HÔM NAY
        // ==========================================================
        public async Task<IActionResult> Dashboard()
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            if (driverId == null) return RedirectToAction("Login", "Auth");

            var today = DateTime.Today;

            var orders = await _context.Orders
                .Where(o => o.DriverId == driverId && o.DeliveryDate == today)
                .OrderBy(o =>
                    o.Status == "pending" ? 1 :
                    o.Status == "shipping" ? 2 :
                    o.Status == "done" ? 3 :
                    o.Status == "failed" ? 4 : 5
                )
                .ToListAsync();

            return View(orders);
        }

        // ==========================================================
        // 📌 ĐƠN HÀNG CHƯA NHẬN
        // ==========================================================
        public async Task<IActionResult> AvailableOrders()
        {
            if (!IsDriver()) return Forbid();

            var orders = await _context.Orders
                .Where(o => o.DriverId == null && o.Status == "pending")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // 📌 TÀI XẾ NHẬN ĐƠN
        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            if (driverId == null) return Unauthorized();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            order.DriverId = driverId;
            order.AssignedAt = DateTime.Now;
            order.DeliveryDate = DateTime.Today;

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Bạn đã nhận đơn!";
            return RedirectToAction(nameof(AvailableOrders));
        }

        // ==========================================================
        // 📌 CHI TIẾT ĐƠN
        // ==========================================================
        public async Task<IActionResult> StopDetail(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();
            return View(order);
        }

        // ==========================================================
        // 📌 BẮT ĐẦU GIAO
        // ==========================================================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            if (status == "shipping")
                order.Status = "shipping";

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Đã bắt đầu giao!";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================================
        // 📌 GIAO THÀNH CÔNG — MỞ TRANG UPLOAD POD
        // ==========================================================
        public async Task<IActionResult> Delivered(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            return View(new DeliveredViewModel
            {
                OrderId = order.Id,
                Code = order.Code,
                ReceiverName = order.ReceiverName,
                ReceiverAddress = order.ReceiverAddress
            });
        }

        // 📌 LƯU POD
        [HttpPost]
        public async Task<IActionResult> Delivered(DeliveredViewModel model)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.DriverId == driverId);

            if (order == null) return NotFound();

            if (model.PodImage == null)
            {
                ModelState.AddModelError("PodImage", "Bạn phải tải lên ảnh POD!");
                return View(model);
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads/pod");
            Directory.CreateDirectory(folder);

            var fileName = $"{order.Code}_POD_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await model.PodImage.CopyToAsync(stream);

            order.PodImagePath = "/uploads/pod/" + fileName;
            order.DeliveredAt = DateTime.Now;
            order.Status = "done";

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Đã giao hàng! Vui lòng thu COD nếu có.";
            return RedirectToAction("StopDetail", new { id = order.Id });
        }

        // ==========================================================
        // 💰 XÁC NHẬN ĐÃ THU COD
        // ==========================================================
        public async Task<IActionResult> ConfirmCOD(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            order.IsCodPaid = true;
            order.CodPaidAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Message"] = "💰 Đã thu COD!";
            return RedirectToAction("StopDetail", new { id });
        }

        // ==========================================================
        // 📌 GIAO THẤT BẠI
        // ==========================================================
        public async Task<IActionResult> Failed(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            return View(new FailedDeliveryViewModel
            {
                OrderId = order.Id,
                Code = order.Code,
                ReceiverName = order.ReceiverName,
                ReceiverAddress = order.ReceiverAddress
            });
        }

        [HttpPost]
        public async Task<IActionResult> Failed(FailedDeliveryViewModel model)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.DriverId == driverId);

            if (order == null) return NotFound();

            order.FailedReason = model.FailedReason;
            order.FailedAt = DateTime.Now;
            order.Status = "failed";

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Đã lưu giao thất bại!";
            return RedirectToAction(nameof(Dashboard));
        }

        // ==========================================================
        // 📜 LỊCH SỬ GIAO HÀNG
        // ==========================================================
        public async Task<IActionResult> History(DateTime? day, string status = "all")
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            if (driverId == null) return Unauthorized();

            var query = _context.Orders
                .Where(o => o.DriverId == driverId && (o.Status == "done" || o.Status == "failed"));

            if (day.HasValue)
            {
                var d = day.Value.Date;
                query = query.Where(o =>
                    o.DeliveredAt.HasValue &&
                    o.DeliveredAt.Value.Date == d
                );
            }

            if (status != "all")
                query = query.Where(o => o.Status == status);

            query = query.OrderByDescending(o => o.DeliveredAt);

            ViewBag.Day = day?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            return View(await query.ToListAsync());
        }
    }
}
