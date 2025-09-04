using System.Threading;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.App.ViewModels
{
    public class GuideViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _root;
        private readonly CancellationTokenSource _cts = new();

        public RelayCommand ContinueCommand { get; }

        public GuideViewModel(MainWindowViewModel root)
        {
            _root = root;
            ContinueCommand = new RelayCommand(GoNext);

            // Tự động chuyển sau 2 giây (nếu người dùng chưa bấm)
            _ = AutoAdvanceAsync(_cts.Token);
        }

        private async Task AutoAdvanceAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(2000, token);
                if (!token.IsCancellationRequested)
                    _root.NavigateToPayment();
            }
            catch (TaskCanceledException) { /* user đã bấm Tiếp tục */ }
        }

        private void GoNext()
        {
            // Người dùng chủ động → hủy auto-chuyển (nếu còn)
            if (!_cts.IsCancellationRequested) _cts.Cancel();
            _root.NavigateToPayment();
        }
    }
}