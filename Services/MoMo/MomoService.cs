using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WedNightFury.Models.MoMo;

namespace WedNightFury.Services.MoMo
{
    public class MomoService : IMomoService
    {
        private readonly IConfiguration _configuration;

        public MomoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GIẢ LẬP: Không gọi MoMo thật, chỉ build URL quay về MomoReturn
        public string CreatePaymentUrl(MomoPaymentRequest model, HttpContext httpContext)
        {
            // Giả lập: cho rằng lúc nào cũng redirect user sang 1 "trang thanh toán"
            // Ở đồ án, ta cho user quay thẳng về MomoReturn với resultCode = 0 (thành công)

            var request = httpContext.Request;
            var scheme  = request.Scheme;                      // http/https
            var host    = request.Host.Value;                  // localhost:5000

            // Tự build URL giống như MoMo trả về:
            // /Order/MomoReturn?orderId=...&amount=...&resultCode=0&message=Success
            var url =
                $"{scheme}://{host}/Order/MomoReturn" +
                $"?orderId={model.OrderId}" +
                $"&amount={model.Amount}" +
                $"&resultCode=0" +
                $"&message=Thanh+toan+MoMo+fake+thanh+cong";

            return url;
        }

        // Vẫn dùng để parse query ở MomoReturn
        public MomoResultModel ProcessPaymentResponse(IQueryCollection collection)
        {
            var resultCode = collection["resultCode"].ToString();
            var success    = resultCode == "0";

            var raw = string.Join("&", collection.Select(kv => $"{kv.Key}={kv.Value}"));

            var result = new MomoResultModel
            {
                Success    = success,
                ResultCode = resultCode,
                Message    = collection["message"],
                OrderId    = collection["orderId"],
                TransId    = collection["transId"],
                Amount     = long.TryParse(collection["amount"], out var amount) ? amount : 0,
                RawData    = raw
            };

            return result;
        }
    }
}
