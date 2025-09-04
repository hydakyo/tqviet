using System.Threading.Tasks;
using System.Windows.Controls;
using PhotoBoothKiosk.App.Views;
using PhotoBoothKiosk.Core.Services;

namespace PhotoBoothKiosk.App.ViewModels
{
    /// <summary>
    /// ViewModel gốc: quản lý điều hướng giữa các màn hình và khởi tạo Service tầng Core.
    /// </summary>
    public class MainWindowViewModel : BaseViewModel
    {
        // View hiện tại để ContentControl hiển thị
        private UserControl _currentView;
        public UserControl CurrentView
        {
            get => _currentView;
            private set => Set(ref _currentView, value);
        }

        // Services dùng chung
        public SessionManager SessionManager { get; }
        public PaymentService  PaymentService  { get; }

        // Các View (để tái sử dụng instance)
        private readonly WelcomeView   _welcome  = new();
        private readonly GuideView     _guide    = new();
        private readonly PaymentView   _payment  = new();
        private readonly SessionView   _session  = new();
        private readonly ThankYouView  _thanks   = new();

        public MainWindowViewModel()
        {
            // === Khởi tạo Services (tầng Core) ===
            var cfg  = App.Config;
            var dslr = new DslrBoothController(
                cfg.DslrBooth.ProcessName,
                cfg.DslrBooth.KeyboardSequence,
                cfg.DslrBooth.OutputFolder,
                cfg.DslrBooth.Mode);

            SessionManager = new SessionManager(dslr);
            PaymentService = new PaymentService(cfg.Payment.DefaultProvider, cfg.Payment.DefaultAmount);

            // === Gán DataContext cho từng View ===
            _welcome.DataContext = new WelcomeViewModel(this);
            _guide.DataContext   = new GuideViewModel(this);
            _payment.DataContext = new PaymentViewModel(this);
            _session.DataContext = new SessionViewModel(this);
            _thanks.DataContext  = new ThankYouViewModel(this);

            // Màn hình khởi đầu
            CurrentView = _welcome;

            // === Khi ảnh sẵn sàng trong output của DSLRBooth ===
            // Cho khách thao tác/in/QR/email trên UI của DSLRBooth vài giây, sau đó chuyển ThankYou
            SessionManager.OnPhotoReady += async (s, photo) =>
            {
                await Task.Delay(5000); // tuỳ chỉnh nếu muốn cho khách xem lâu hơn
                NavigateToThankYou();
            };
        }

        // ===============================
        //      HÀM ĐIỀU HƯỚNG PUBLIC
        // ===============================
        public void NavigateToWelcome()  => CurrentView = _welcome;
        public void NavigateToGuide()    => CurrentView = _guide;
        public void NavigateToPayment()  => CurrentView = _payment;
        public void NavigateToSession()  => CurrentView = _session;
        public void NavigateToThankYou() => CurrentView = _thanks;
    }
}