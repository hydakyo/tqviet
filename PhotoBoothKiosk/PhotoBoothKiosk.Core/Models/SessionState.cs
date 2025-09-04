namespace PhotoBoothKiosk.Core.Models
{
    /// <summary>
    /// Trạng thái phiên chụp ảnh của kiosk.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Nhàn rỗi, chưa có phiên.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Đang trong phiên (đã bắt đầu quy trình chụp/đếm ngược).
        /// </summary>
        InSession = 1,

        /// <summary>
        /// Đang xử lý (DSLRBooth đang render/in/chia sẻ).
        /// </summary>
        Processing = 2
    }
}