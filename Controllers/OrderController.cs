using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WedNightFury.Models;
using WedNightFury.Models.VnPay;
using WedNightFury.Models.MoMo;
using WedNightFury.Services.VnPay;
using WedNightFury.Services.MoMo;

namespace WedNightFury.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IVnPayService _vnPayService;
        private readonly IMomoService _momoService;

        public OrderController(
            AppDbContext context,
            IVnPayService vnPayService,
            IMomoService momoService)
        {
            _context = context;
            _vnPayService = vnPayService;
            _momoService = momoService;
        }

        // =========================================================
        // GET /Order/Create – KHÁCH TẠO ĐƠN
        // =========================================================
        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var model = new Order();

            var profile = _context.Profiles.FirstOrDefault(p => p.UserId == userId.Value);
            if (profile != null)
            {
                model.SenderName    = profile.FullName;
                model.SenderPhone   = profile.Phone;
                model.SenderAddress = profile.Address;

                ViewBag.SenderCity = profile.City;
            }
            else
            {
                ViewBag.SenderCity = "";
            }

            return View(model);
        }

        // =========================================================
        // POST /Order/Create – LƯU ĐƠN (không thanh toán online)
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

            MapFormToOrder(model);

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

        // =========================================================
        // POST /Order/CreateAndPayVnPay – LƯU ĐƠN + CHUYỂN SANG VNPay
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAndPayVnPay(Order model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            MapFormToOrder(model);

            model.Code       = $"NF-{DateTime.Now:yyyyMMddHHmmss}";
            model.Status     = "pending";
            model.CreatedAt  = DateTime.Now;
            model.CustomerId = userId.Value;

            _context.Orders.Add(model);
            _context.SaveChanges();

            var paymentInfo = new PaymentInformationModel
            {
                Amount           = (long)model.ShipFee,
                OrderId          = model.Id.ToString(),
                OrderDescription = $"Thanh toán phí vận chuyển đơn #{model.Code}",
                Name             = model.SenderName ?? "",
                OrderType        = "billpayment"
            };

            var paymentUrl = _vnPayService.CreatePaymentUrl(paymentInfo, HttpContext);
            return Redirect(paymentUrl);
        }

        // =========================================================
        // POST /Order/CreateAndPayMomo – THANH TOÁN MOMO TRƯỚC,
        // CHƯA LƯU ĐƠN, CHỈ LƯU VÀO SESSION
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAndPayMomo(Order model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Map thêm các field không binding trực tiếp
            MapFormToOrder(model);

            // KHÔNG set Code / CreatedAt / CustomerId / Status ở đây
            // Để dành tới lúc người dùng xác nhận sau khi thanh toán xong

            if (model.ShipFee <= 0)
            {
                TempData["Error"] = "Đơn hàng chưa có phí ship để thanh toán bằng MoMo.";
                return RedirectToAction("Create");
            }

            // Lưu tạm dữ liệu đơn vào Session
            var json = JsonSerializer.Serialize(model);
            HttpContext.Session.SetString("TempOrderData", json);

            // Gửi request sang MoMo (test)
            var momoRequest = new MomoPaymentRequest
            {
                OrderId   = Guid.NewGuid().ToString(), // mã giao dịch gửi MoMo (không phải Id đơn DB)
                Amount    = (long)model.ShipFee,
                OrderInfo = $"Thanh toán phí vận chuyển NightFury",
                ExtraData = ""
            };

            var payUrl = _momoService.CreatePaymentUrl(momoRequest, HttpContext);
            return Redirect(payUrl);
        }

        // =========================================================
        // HÀM PHỤ MAP FORM → ORDER
        // =========================================================
        private void MapFormToOrder(Order model)
        {
            model.GoodsType    = Request.Form["GoodsType"];
            model.AreaType     = Request.Form["AreaType"];
            model.PickupMethod = Request.Form["PickupMethod"];
            model.ServiceLevel = Request.Form["ServiceLevel"];
            model.ShipPayer    = Request.Form["ShipPayer"];

            model.Value     = ParseDecimal(Request.Form["Value"]);
            model.CodAmount = ParseDecimal(Request.Form["CodAmount"]);
            model.ShipFee   = ParseDecimal(Request.Form["ShipFee"]);

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

            model.Province = province;
        }

        private decimal ParseDecimal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            raw = raw.Replace(".", "").Replace(",", "");
            return decimal.TryParse(raw, out var v) ? v : 0;
        }

        // =========================================================
        // /Order/Success – ĐƠN TẠO THƯỜNG (KHÔNG ONLINE)
        // =========================================================
        public IActionResult Success()
        {
            ViewBag.OrderId   = TempData["OrderId"];
            ViewBag.OrderCode = TempData["OrderCode"];
            ViewBag.Error     = TempData["Error"];
            return View();
        }

        // =========================================================
        // /Order/VnPayReturn – Callback từ VNPay
        // =========================================================
        public IActionResult VnPayReturn()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);

            Order? order = null;
            if (int.TryParse(response.OrderId, out var orderId))
            {
                order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
                if (order != null && !response.Success)
                {
                    order.Status = "failed";
                    _context.SaveChanges();
                }

                if (order != null && response.Success)
                {
                    order.Status = "paid_vnpay";
                    _context.SaveChanges();
                }
            }

            ViewBag.Order         = order;
            ViewBag.VnPayResponse = response;
            ViewBag.IsSuccess     = response.Success;
            ViewBag.VnPayMessage  = response.Success
                ? "Thanh toán VNPay thành công."
                : $"Thanh toán VNPay thất bại. Mã lỗi: {response.VnPayResponseCode}";

            return View("VnPayResult");
        }

        // =========================================================
        // /Order/MomoReturn – Callback từ MoMo:
        // CHỈ HIỂN THỊ KẾT QUẢ, CHƯA LƯU ĐƠN
        // =========================================================
        public IActionResult MomoReturn()
        {
            var result = _momoService.ProcessPaymentResponse(Request.Query);

            ViewBag.Message = result.Success
                ? "Thanh toán MoMo thành công. Nhấn 'Xác nhận & Tạo đơn' để lưu đơn."
                : "Thanh toán MoMo thất bại. Bạn có thể tạo lại đơn hoặc chọn phương thức khác.";

            return View("MomoResult", result);
        }

        // =========================================================
        // /Order/SaveOrderAfterMomo – SAU KHI THANH TOÁN THÀNH CÔNG
        // VÀ BẤM XÁC NHẬN TRÊN MomoResult THÌ MỚI LƯU ĐƠN
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveOrderAfterMomo()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var json = HttpContext.Session.GetString("TempOrderData");
            if (string.IsNullOrEmpty(json))
            {
                TempData["Error"] = "Không tìm thấy dữ liệu đơn hàng trong phiên làm việc. Vui lòng tạo lại đơn.";
                return RedirectToAction("Create");
            }

            var tempOrder = JsonSerializer.Deserialize<Order>(json);
            if (tempOrder == null)
            {
                TempData["Error"] = "Dữ liệu đơn hàng không hợp lệ. Vui lòng tạo lại đơn.";
                return RedirectToAction("Create");
            }

            tempOrder.Code       = $"NF-{DateTime.Now:yyyyMMddHHmmss}";
            tempOrder.Status     = "pending";      // đơn đã thanh toán phí ship, chờ xử lý
            tempOrder.CreatedAt  = DateTime.Now;
            tempOrder.CustomerId = userId.Value;

            _context.Orders.Add(tempOrder);
            _context.SaveChanges();

            // Xóa dữ liệu tạm trong Session
            HttpContext.Session.Remove("TempOrderData");

            TempData["Success"] = $"Đã tạo đơn #{tempOrder.Code} sau khi thanh toán phí ship MoMo.";
            return RedirectToAction("Details", new { id = tempOrder.Id });
        }

        // =========================================================
        // /Order/MomoNotify – IPN từ MoMo
        // =========================================================
        [HttpPost]
        public IActionResult MomoNotify()
        {
            return Ok();
        }

        // =========================================================
        // /Order/Manage – QUẢN LÝ VẬN ĐƠN
        // =========================================================
        public IActionResult Manage(string? status, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (status == "cancelled")
                {
                    q = q.Where(o => o.Status == "cancelled" || o.Status == "failed");
                }
                else
                {
                    q = q.Where(o => o.Status == status);
                }
            }

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

            ViewBag.TotalOrders     = q.Count();
            ViewBag.PendingOrders   = q.Count(o => o.Status == "pending");
            ViewBag.ShippingOrders  = q.Count(o => o.Status == "shipping");
            ViewBag.DoneOrders      = q.Count(o => o.Status == "done");
            ViewBag.CancelledOrders = q.Count(o => o.Status == "cancelled" || o.Status == "failed");

            var list = q.OrderByDescending(o => o.CreatedAt).ToList();
            return View(list);
        }

        // =========================================================
        // /Order/Pending – ĐƠN PENDING + SHIPPING
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
        // /Order/Details/{id}
        // =========================================================
        public IActionResult Details(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }

        // =========================================================
        // POST /Order/CompleteAndNew – HOÀN THÀNH ĐƠN & TẠO ĐƠN MỚI
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteAndNew(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            order.Status = "done";
            if (!order.DeliveredAt.HasValue)
            {
                order.DeliveredAt = DateTime.Now;
            }

            _context.SaveChanges();

            TempData["Success"] = $"Đã hoàn thành đơn #{order.Code}. Bạn có thể tạo đơn mới.";
            return RedirectToAction("Create");
        }

        // =========================================================
        // POST /Order/UpdateStatus – ĐỔI TRẠNG THÁI TỪ MÀN MANAGE
        // =========================================================
        [HttpPost]
        public IActionResult UpdateStatus(int id, string newStatus)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            order.Status = newStatus;

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
