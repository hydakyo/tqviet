using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.PaymentProviders
{
    public interface IPaymentProvider
    {
        /// <summary>
        /// Thực hiện thanh toán với số tiền (VND).
        /// </summary>
        Task<PaymentResult> PayAsync(int amount);
    }
}