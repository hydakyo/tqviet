using System.Threading;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.App.ViewModels
{
    public class ThankYouViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _root;
        private readonly CancellationTokenSource _cts = new();

        public RelayCommand BackToHomeCommand { get; }

        public ThankYouViewModel(MainWindowViewModel root)
        {
            _root = root;

            BackToHomeCommand = new RelayCommand(GoHome);

            // Tự động quay về sau 5 giây
            _ = AutoBackAsync(_cts.Token);
        }

        private async Task AutoBackAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(5000, token);
                if (!token.IsCancellationRequested)
                    _root.NavigateToWelcome();
            }
            catch (TaskCanceledException)
            {
                // Người dùng đã bấm "Về màn hình chính" → bỏ qua
            }
        }

        private void GoHome()
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _root.NavigateToWelcome();
        }
    }
}