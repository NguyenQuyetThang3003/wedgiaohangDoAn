using Microsoft.AspNetCore.Http;
using WedNightFury.Models.MoMo;

namespace WedNightFury.Services.MoMo
{
    public interface IMomoService
    {
        // Tạo URL thanh toán MoMo
        string CreatePaymentUrl(MomoPaymentRequest model, HttpContext httpContext);

        // Xử lý kết quả redirect / notify từ MoMo
        MomoResultModel ProcessPaymentResponse(IQueryCollection collection);
    }
}
