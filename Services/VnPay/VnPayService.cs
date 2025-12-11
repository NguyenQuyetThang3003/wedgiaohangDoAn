using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WedNightFury.Libraries;
using WedNightFury.Models.VnPay;

namespace WedNightFury.Services.VnPay
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;

        public VnPayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
        {
            // ----- Fallback cho TimeZoneId -----
            // Nếu có cấu hình "TimeZoneId" -> dùng, nếu không -> dùng TimeZoneInfo.Local
            var timeZoneId = _configuration["TimeZoneId"];
            TimeZoneInfo timeZoneById;

            if (!string.IsNullOrEmpty(timeZoneId))
            {
                try
                {
                    timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    timeZoneById = TimeZoneInfo.Local;
                }
                catch (InvalidTimeZoneException)
                {
                    timeZoneById = TimeZoneInfo.Local;
                }
            }
            else
            {
                // Không có cấu hình -> dùng Local
                timeZoneById = TimeZoneInfo.Local;
            }

            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            var tick = DateTime.Now.Ticks.ToString();
            var pay = new VnPayLibrary();
            var urlCallBack = _configuration["Vnpay:PaymentBackReturnUrl"];

            pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
            pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
            pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
            pay.AddRequestData("vnp_Amount", ((long)model.Amount * 100).ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);
            pay.AddRequestData("vnp_OrderInfo", $"{model.Name} {model.OrderDescription} {model.Amount}");
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", tick);

            var paymentUrl = pay.CreateRequestUrl(
                _configuration["Vnpay:BaseUrl"],
                _configuration["Vnpay:HashSecret"]
            );

            return paymentUrl;
        }

        public PaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(
                collections,
                _configuration["Vnpay:HashSecret"]
            );

            return response;
        }
    }
}
