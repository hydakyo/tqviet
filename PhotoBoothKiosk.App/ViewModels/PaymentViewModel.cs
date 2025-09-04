using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.App.ViewModels
{
    public class PaymentViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _root;

        // ======= STATE =======
        private string _status = "Chọn số ảnh rồi chọn phương thức thanh toán";
        public string Status { get => _status; set => Set(ref _status, value); }

        private int _printCount = 1;
        public int PrintCount
        {
            get => _printCount;
            set { if (Set(ref _printCount, Math.Max(1, value))) Raise(nameof(PriceText)); }
        }

        private int _amountVnd;
        public int AmountVnd
        {
            get => _amountVnd;
            private set { if (Set(ref _amountVnd, Math.Max(0, value))) Raise(nameof(PriceText)); }
        }

        public string PriceText
            => $"Số lượng: {PrintCount} | Thành tiền: {AmountVnd.ToString("N0", CultureInfo.GetCultureInfo(\"vi-VN\"))} đ";

        private bool _isPaying;
        public bool IsPaying
        {
            get => _isPaying;
            private set
            {
                if (Set(ref _isPaying, value))
                {
                    PayDemoCommand.RaiseCanExecuteChanged();
                    PayCashCommand.RaiseCanExecuteChanged();
                    PayQrCommand.RaiseCanExecuteChanged();
                    PayPosCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // ======= COMMANDS =======
        public RelayCommand PayDemoCommand { get; }
        public RelayCommand PayCashCommand { get; }
        public RelayCommand PayQrCommand { get; }
        public RelayCommand PayPosCommand { get; }

        public PaymentViewModel(MainWindowViewModel root)
        {
            _root = root;

            PayDemoCommand = new RelayCommand(() => PayAsync("Demo"), () => !IsPaying && AmountVnd >= 0);
            PayCashCommand = new RelayCommand(() => PayAsync("Cash"), () => !IsPaying && AmountVnd > 0);
            PayQrCommand   = new RelayCommand(() => PayAsync("Qr"),   () => !IsPaying && AmountVnd > 0);
            PayPosCommand  = new RelayCommand(() => PayAsync("Pos"),  () => !IsPaying && AmountVnd > 0);

            // Mặc định: 1 tấm
            UpdateAmount(1);
        }

        // ======= PUBLIC API (được gọi từ View code-behind) =======
        public void SelectCount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!int.TryParse(text, out var n) || n <= 0) return;
            UpdateAmount(n);
        }

        // ======= CORE LOGIC =======
        private void UpdateAmount(int count)
        {
            PrintCount = count;

            var cfg = App.Config.Pricing;
            // Nếu trùng gói trong Bundles → dùng giá gói, ngược lại: count * PricePerPrint
            var bundle = cfg.Bundles?.FirstOrDefault(b => b.Count == count);
            AmountVnd = bundle?.Price ?? (count * cfg.PricePerPrint);

            Status = "Chọn phương thức thanh toán";
        }

        private async void PayAsync(string provider)
        {
            try
            {
                IsPaying = true;
                Status = "Đang xác nhận thanh toán…";

                // Cho phép dùng DefaultProvider khi provider rỗng
                var prov = string.IsNullOrWhiteSpace(provider) ? App.Config.Payment.DefaultProvider : provider;

                // Gọi PaymentService (tầng Core) với số tiền đã tính
                var result = await _root.PaymentService.PayAsync(prov, AmountVnd);

                if (result.Success)
                {
                    Status = "Thanh toán thành công!";
                    await Task.Delay(1500);
                    _root.NavigateToSession();
                }
                else
                {
                    Status = string.IsNullOrWhiteSpace(result.Message)
                        ? "Thanh toán thất bại, vui lòng thử lại!"
                        : $"Thanh toán thất bại: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                Status = $"Lỗi thanh toán: {ex.Message}";
            }
            finally
            {
                IsPaying = false;
            }
        }
    }
}