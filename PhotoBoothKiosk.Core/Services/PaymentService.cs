using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;
using PhotoBoothKiosk.Core.PaymentProviders;

namespace PhotoBoothKiosk.Core.Services
{
    /// <summary>
    /// PaymentService: điều phối thanh toán qua provider (Cash/QR/POS/Demo).
    /// </summary>
    public sealed class PaymentService
    {
        private readonly Dictionary<string, IPaymentProvider> _providers;
        private readonly string _defaultProvider;
        private readonly int _defaultAmount;

        public PaymentService(string defaultProvider, int defaultAmount)
        {
            _defaultProvider = defaultProvider ?? "Demo";
            _defaultAmount   = defaultAmount;

            _providers = new Dictionary<string, IPaymentProvider>(StringComparer.OrdinalIgnoreCase)
            {
                { "Demo", new DemoPaymentProvider() },
                { "Cash", new CashPaymentProvider() },
                { "Qr",   new QrPaymentProvider() },
                { "Pos",  new PosPaymentProvider() }
            };
        }

        /// <summary>
        /// Thực hiện thanh toán bất đồng bộ qua provider.
        /// </summary>
        /// <param name="provider">Tên provider ("Demo", "Cash", "Qr", "Pos")</param>
        /// <param name="amount">Số tiền (VND)</param>
        /// <returns>PaymentResult</returns>
        public async Task<PaymentResult> PayAsync(string? provider, int amount)
        {
            var name = string.IsNullOrWhiteSpace(provider) ? _defaultProvider : provider;
            var amt  = amount > 0 ? amount : _defaultAmount;

            if (!_providers.TryGetValue(name, out var impl))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = $"Provider '{name}' chưa được hỗ trợ"
                };
            }

            try
            {
                return await impl.PayAsync(amt);
            }
            catch (Exception ex)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = $"Lỗi provider {name}: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Đăng ký thêm provider mới (vd: MoMo, VNPay).
        /// </summary>
        public void RegisterProvider(string name, IPaymentProvider provider)
        {
            if (string.IsNullOrWhiteSpace(name) || provider is null)
                throw new ArgumentNullException();

            _providers[name] = provider;
        }
    }
}