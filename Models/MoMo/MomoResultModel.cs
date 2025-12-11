namespace WedNightFury.Models.MoMo
{
    // Model hiển thị trên trang MomoResult.cshtml
    public class MomoResultModel
    {
        // true nếu resultCode == "0"
        public bool Success { get; set; }

        // Mã kết quả MoMo trả về (0 = thành công, khác 0 = lỗi)
        public string ResultCode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;

        public string TransId { get; set; } = string.Empty;

        public long Amount { get; set; }

        // Lưu toàn bộ query string để debug
        public string RawData { get; set; } = string.Empty;
    }
}
