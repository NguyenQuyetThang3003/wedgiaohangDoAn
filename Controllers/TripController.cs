using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class TripController : Controller
{
    private readonly AppDbContext _context;

    public TripController(AppDbContext context)
    {
        _context = context;
    }

    private int? GetDriverId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }

    // ============ API GEOCODING – Lấy Lat/Lng từ địa chỉ ============
    public async Task<(double? lat, double? lng)> GetLatLngFromAddress(string address)
    {
        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(address)}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NightFuryDriverApp/1.0");

            var json = await client.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<List<NominatimResult>>(json);

            if (data == null || data.Count == 0)
                return (null, null);

            return (double.Parse(data[0].lat), double.Parse(data[0].lon));
        }
        catch
        {
            return (null, null);
        }
    }

    public class NominatimResult
    {
        public string lat { get; set; }
        public string lon { get; set; }
    }

    // ============ LỘ TRÌNH HÔM NAY – SẮP XẾP TIẾNG VIỆT ============
    public IActionResult Today()
    {
        int? driverId = GetDriverId();
        if (driverId == null)
            return RedirectToAction("Login", "Auth");

        var today = DateTime.Today;

        var orders = _context.Orders
            .Where(o => o.DriverId == driverId && o.DeliveryDate == today)
            .AsEnumerable() // để custom sort
            .OrderBy(o =>
                o.Status == "shipping" ? 1 :     // Đang giao
                o.Status == "pending" ? 2 :      // Chờ giao
                o.Status == "done"    ? 3 :      // Đã giao
                o.Status == "failed"  ? 4 : 99   // Thất bại
            )
            .ThenBy(o => o.Sequence)
            .ToList();

        return View(orders);
    }

    // ============ CHI TIẾT ĐIỂM GIAO ============
    public async Task<IActionResult> StopDetail(int id)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null) return NotFound();

        // Tự động geocoding nếu chưa có tọa độ
        if (order.Lat == null || order.Lng == null)
        {
            var (lat, lng) = await GetLatLngFromAddress(order.ReceiverAddress ?? "");

            order.Lat = lat;
            order.Lng = lng;

            _context.SaveChanges();
        }

        return View(order);
    }

    // ============ GIAO THÀNH CÔNG (POD) ============
    [HttpPost]
    public async Task<IActionResult> CompleteDelivery(int id, IFormFile podImage)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null) return NotFound();

        string imagePath = null;

        if (podImage != null)
        {
            string fileName = $"POD_{id}_{DateTime.Now.Ticks}.jpg";
            string folder = "wwwroot/images/pod/";

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string savePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(savePath, FileMode.Create);
            await podImage.CopyToAsync(stream);

            imagePath = "/images/pod/" + fileName;
        }

        order.PodImagePath = imagePath;
        order.Status = "done";
        order.DeliveredAt = DateTime.Now;
        order.DeliveredNote = "Giao thành công";

        _context.SaveChanges();
        return RedirectToAction("Today");
    }

    // ============ GIAO THẤT BẠI ============
    [HttpPost]
    public IActionResult FailedDelivery(int id, string reason)
    {
        var order = _context.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null) return NotFound();

        order.Status = "failed";
        order.FailedReason = reason;
        order.FailedAt = DateTime.Now;

        _context.SaveChanges();
        return RedirectToAction("Today");
    }
}
