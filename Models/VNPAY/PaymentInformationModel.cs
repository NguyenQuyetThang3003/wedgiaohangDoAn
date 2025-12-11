namespace WedNightFury.Models.VnPay   // dùng đúng VnPay
{
    public class PaymentInformationModel
    {
        public long Amount { get; set; }
        public string OrderId { get; set; } = "";
        public string OrderDescription { get; set; } = "";
        public string Name { get; set; } = "";
        public string OrderType { get; set; } = "";
    }
}
