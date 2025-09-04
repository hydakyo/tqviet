using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.PaymentProviders
{
    /// <summary>
    /// Thanh toán bằng tiền mặt (Bill Acceptor).
    /// Hiện tại là stub: giả lập thành công sau 1s.
    /// Thực tế cần tích hợp SDK của thiết bị.
    /// </summary>
    public class CashPaymentProvider : IPaymentProvider
    {
        public async Task<PaymentResult> PayAsync(int amount)
        {
            // TODO: Gọi SDK/driver thật để nhận tín hiệu tiền mặt đã nạp
            await Task.Delay(1000);
            return new PaymentResult
            {
                Success = true,
                Message = $"Thanh toán tiền mặt {amount}đ thành công",
                TransactionId = "CASH-" + System.Guid.NewGuid().ToString("N")
            };
        }
    }
}