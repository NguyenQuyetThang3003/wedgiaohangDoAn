using Microsoft.AspNetCore.Http;
using WedNightFury.Models.VnPay;

namespace WedNightFury.Services.VnPay
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
        PaymentResponseModel PaymentExecute(IQueryCollection collections);
    }
}
