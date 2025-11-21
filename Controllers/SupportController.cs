using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;

public class SupportController : Controller
{
    private readonly AppDbContext _context;

    public SupportController(AppDbContext context)
    {
        _context = context;
    }

    private int? GetDriverId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }

    // ============ VIEW FORM GỬI HỖ TRỢ ============
    public IActionResult Index()
    {
        return View();
    }

    // ============ LƯU TICKET ============
    [HttpPost]
    public IActionResult Create(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Vui lòng nhập nội dung hỗ trợ.";
            return RedirectToAction("Index");
        }

        int? driverId = GetDriverId();
        if (driverId == null)
        {
            TempData["Error"] = "Không tìm thấy tài xế.";
            return RedirectToAction("Index");
        }

        var ticket = new SupportTicket
        {
            DriverId = driverId.Value,
            Message = message,
            Status = "pending",
            CreatedAt = DateTime.Now
        };

        _context.SupportTickets.Add(ticket);
        _context.SaveChanges();

        TempData["Message"] = "📩 Gửi yêu cầu thành công!";
        return RedirectToAction("History");
    }

    // ============ LỊCH SỬ ============
    public IActionResult History()
    {
        int? driverId = GetDriverId();
        if (driverId == null) return RedirectToAction("Index", "Auth");

        var list = _context.SupportTickets
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return View(list);
    }
}
