using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.PaymentProviders
{
    /// <summary>
    /// Thanh toán QR Code (MoMo/VNPay/ZaloPay).
    /// Hiện tại là stub: giả lập thành công sau 1.5s.
    /// Thực tế cần gọi API nhà cung cấp.
    /// </summary>
    public class QrPaymentProvider : IPaymentProvider
    {
        public async Task<PaymentResult> PayAsync(int amount)
        {
            // TODO: Gọi API QR để sinh mã, hiển thị cho khách quét và xác minh callback
            await Task.Delay(1500);
            return new PaymentResult
            {
                Success = true,
                Message = $"Thanh toán QR {amount}đ thành công",
                TransactionId = "QR-" + System.Guid.NewGuid().ToString("N")
            };
        }
    }
}