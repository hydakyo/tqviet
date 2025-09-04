using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.PaymentProviders
{
    /// <summary>
    /// Thanh toán POS (quẹt thẻ).
    /// Hiện tại là stub: giả lập thành công sau 2s.
    /// Thực tế cần SDK POS terminal.
    /// </summary>
    public class PosPaymentProvider : IPaymentProvider
    {
        public async Task<PaymentResult> PayAsync(int amount)
        {
            // TODO: Tích hợp SDK POS terminal thật
            await Task.Delay(2000);
            return new PaymentResult
            {
                Success = true,
                Message = $"Thanh toán POS {amount}đ thành công",
                TransactionId = "POS-" + System.Guid.NewGuid().ToString("N")
            };
        }
    }
}
