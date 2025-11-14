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

        // ========== Lấy userId tài xế ==========
        private int? GetCurrentDriverId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        // ========== Kiểm tra tài xế ==========
        private bool IsDriver()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role == null) return false;

            role = role.ToLower().Trim();

            return role == "driver" || role == "taixe";
        }

        // =======================================================
        // 📌 1. TRANG XEM CÁC ĐƠN CHƯA NHẬN (Tài xế tự nhận như Grab)
        // =======================================================
        public async Task<IActionResult> AvailableOrders()
        {
            if (!IsDriver()) return Forbid();

            var orders = await _context.Orders
                .Where(o => o.DriverId == null && o.Status == "pending")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // 📌 Nhận đơn
        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            if (driverId == null) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.DriverId != null)
            {
                TempData["Message"] = "❌ Đơn đã có tài xế khác nhận!";
                return RedirectToAction(nameof(AvailableOrders));
            }

            order.DriverId = driverId;
            order.DeliveryDate = DateTime.Today;
            order.Sequence = 1;
            order.Status = "pending";

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Nhận đơn thành công!";
            return RedirectToAction(nameof(Dashboard));
        }

        // =======================================================
        // 📌 2. DASHBOARD – ĐƠN CỦA TÀI XẾ HÔM NAY
        // =======================================================
        public async Task<IActionResult> Dashboard()
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();
            if (driverId == null) return RedirectToAction("Login", "Auth");

            var today = DateTime.Today;

            // Sắp xếp theo trạng thái như yêu cầu:
            // pending → shipping → done → failed
            var orders = await _context.Orders
                .Where(o => o.DriverId == driverId && o.DeliveryDate == today)
                .OrderBy(o =>
                    o.Status == "pending" ? 1 :
                    o.Status == "shipping" ? 2 :
                    o.Status == "done" ? 3 :
                    o.Status == "failed" ? 4 : 5
                )
                .ThenByDescending(o => o.CreatedAt) // Đơn mới nhất trong nhóm
                .ToListAsync();

            return View(orders);
        }

        // =======================================================
        // 📌 3. XEM CHI TIẾT ĐƠN
        // =======================================================
        public async Task<IActionResult> StopDetail(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            return View(order);
        }

        // =======================================================
        // 📌 4. BẮT ĐẦU GIAO (pending → shipping)
        // =======================================================
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

        // =======================================================
        // 📌 5. GIAO THÀNH CÔNG (POD)
        // =======================================================
        public async Task<IActionResult> Delivered(int id)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);

            if (order == null) return NotFound();

            var vm = new DeliveredViewModel
            {
                OrderId = order.Id,
                Code = order.Code,
                ReceiverName = order.ReceiverName,
                ReceiverAddress = order.ReceiverAddress
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delivered(DeliveredViewModel model)
        {
            if (!IsDriver()) return Forbid();

            var driverId = GetCurrentDriverId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.DriverId == driverId);

            if (order == null) return NotFound();

            if (model.PodImage == null)
            {
                ModelState.AddModelError("PodImage", "Bạn phải upload ảnh POD!");
                return View(model);
            }

            // Lưu ảnh POD
            var folder = Path.Combine(_env.WebRootPath, "uploads/pod");
            Directory.CreateDirectory(folder);

            var fileName = $"{order.Code}_POD_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
                await model.PodImage.CopyToAsync(stream);

            order.PodImagePath = "/uploads/pod/" + fileName;
            order.DeliveredAt = DateTime.Now;
            order.DeliveredNote = model.Note;
            order.Status = "done";

            await _context.SaveChangesAsync();

            TempData["Message"] = "✔ Giao hàng thành công!";
            return RedirectToAction(nameof(Dashboard));
        }

        // =======================================================
        // 📌 6. GIAO THẤT BẠI
        // =======================================================
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
    }
}
