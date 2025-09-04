using System.Threading.Tasks;

namespace PhotoBoothKiosk.App.ViewModels
{
    public class WelcomeViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _root;

        public RelayCommand StartCommand { get; }

        public WelcomeViewModel(MainWindowViewModel root)
        {
            _root = root;
            StartCommand = new RelayCommand(StartFlow);
        }

        /// <summary>
        /// Hiển thị hướng dẫn nhanh rồi điều hướng sang màn thanh toán.
        /// </summary>
        private async void StartFlow()
        {
            _root.NavigateToGuide();
            await Task.Delay(2000); // thời gian hiển thị hướng dẫn
            _root.NavigateToPayment();
        }
    }
}