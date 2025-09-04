using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.PaymentProviders
{
    /// <summary>
    /// Provider demo: luôn trả thành công, dùng để test nhanh flow.
    /// </summary>
    public class DemoPaymentProvider : IPaymentProvider
    {
        public Task<PaymentResult> PayAsync(int amount)
        {
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                Message = "Demo: thanh toán giả lập thành công",
                TransactionId = "DEMO-" + System.Guid.NewGuid().ToString("N")
            });
        }
    }
}
