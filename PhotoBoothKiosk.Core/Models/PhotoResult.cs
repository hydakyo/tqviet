using System;

namespace PhotoBoothKiosk.Core.Models
{
    /// <summary>
    /// Thông tin về một ảnh đầu ra (từ DSLRBooth).
    /// </summary>
    public class PhotoResult
    {
        /// <summary>
        /// Đường dẫn đầy đủ tới file ảnh.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Tên file (không bao gồm đường dẫn).
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm ảnh được tạo/ghi nhận (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Kích thước file (byte). Có thể dùng để check ảnh có hợp lệ không.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Ghi chú hoặc metadata thêm (nếu cần).
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }
}