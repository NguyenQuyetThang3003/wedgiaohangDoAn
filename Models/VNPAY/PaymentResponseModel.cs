namespace WedNightFury.Models.VnPay
{
    public class PaymentResponseModel
    {
        /// <summary>
        /// Thanh toán thành công hay không
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Tên phương thức thanh toán (VD: VnPay)
        /// </summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả đơn hàng (OrderInfo từ VNPAY)
        /// </summary>
        public string OrderDescription { get; set; } = string.Empty;

        /// <summary>
        /// Mã đơn hàng nội bộ (vnp_TxnRef)
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Mã thanh toán tại VNPAY (vnp_TransactionNo)
        /// </summary>
        public string PaymentId { get; set; } = string.Empty;

        /// <summary>
        /// Mã giao dịch, có thể trùng PaymentId
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Token bảo mật (vnp_SecureHash)
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Mã phản hồi từ VNPAY (vnp_ResponseCode)
        /// </summary>
        public string VnPayResponseCode { get; set; } = string.Empty;
    }
}
