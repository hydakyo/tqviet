namespace PhotoBoothKiosk.Core.Models
{
    /// <summary>
    /// Kết quả thanh toán từ PaymentService/Provider.
    /// </summary>
    public class PaymentResult
    {
        /// <summary>
        /// Thanh toán thành công hay không.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Thông điệp mô tả (thành công/thất bại).
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Mã giao dịch/transaction ID (nếu có).
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Số tiền thanh toán (VND).
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Trạng thái chi tiết (Pending/Success/Failed/Cancelled).
        /// </summary>
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    }
}