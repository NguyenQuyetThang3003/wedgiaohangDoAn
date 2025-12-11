namespace WedNightFury.Models.MoMo
{
    // Thông tin gửi sang MoMo để tạo thanh toán
    public class MomoPaymentRequest
    {
        public long Amount { get; set; }

        public string OrderId { get; set; } = string.Empty;

        public string OrderInfo { get; set; } = string.Empty;

        public string ExtraData { get; set; } = string.Empty;
    }
}
