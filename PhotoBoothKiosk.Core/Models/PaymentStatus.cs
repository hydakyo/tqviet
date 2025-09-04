namespace PhotoBoothKiosk.Core.Models
{
    /// <summary>
    /// Trạng thái chi tiết của giao dịch thanh toán.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// Chưa hoàn tất / đang xử lý.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Thanh toán thành công.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Thanh toán thất bại (lỗi, bị từ chối).
        /// </summary>
        Failed = 2,

        /// <summary>
        /// Người dùng hủy bỏ giao dịch.
        /// </summary>
        Cancelled = 3
    }
}