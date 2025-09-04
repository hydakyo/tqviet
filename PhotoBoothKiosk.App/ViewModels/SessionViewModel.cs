using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.App.ViewModels
{
    public class SessionViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _root;
        private CancellationTokenSource? _cts;

        // ======= STATE =======
        private int _progress;
        public int Progress
        {
            get => _progress;
            private set => Set(ref _progress, Math.Clamp(value, 0, 100));
        }

        private string _status = "Sẵn sàng";
        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (Set(ref _isRunning, value))
                {
                    StartCaptureCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // ======= COMMANDS =======
        public RelayCommand StartCaptureCommand { get; }
        public RelayCommand CancelCommand { get; }

        public SessionViewModel(MainWindowViewModel root)
        {
            _root = root;
            StartCaptureCommand = new RelayCommand(StartWorkflow, () => !IsRunning);
            CancelCommand       = new RelayCommand(CancelWorkflow,   () => IsRunning);
        }

        // ======= WORKFLOW =======
        private async void StartWorkflow()
        {
            if (IsRunning) return;

            IsRunning = true;
            _cts = new CancellationTokenSource();

            try
            {
                // 1) Hướng dẫn pose ngắn
                Status = "Tạo dáng theo hướng dẫn…";
                Progress = 0;
                await DelayCancelable(1200, _cts.Token);

                // 2) Đếm ngược (hiển thị theo % cho ProgressBar; thực tế 3-2-1 có thể overlay ở View nếu muốn)
                Status = "Chuẩn bị…";
                await CountdownAsync(1500, _cts.Token); // tổng ~1.5s cho 3→2→1 (tuỳ chỉnh)

                // 3) Chụp nhiều tấm
                var shots    = Math.Max(1, App.Config.DslrBooth.Shots);
                var interval = Math.Max(250, App.Config.DslrBooth.IntervalMs); // an toàn: >= 250ms

                for (int s = 1; s <= shots; s++)
                {
                    ThrowIfCanceled();

                    Status = $"Chụp ảnh {s}/{shots}…";
                    await _root.SessionManager.TriggerCaptureAsync();

                    // Nếu chưa phải tấm cuối, chờ interval
                    if (s < shots)
                        await DelayCancelable(interval, _cts.Token);
                }

                // 4) Chờ xử lý trong DSLRBooth
                Status = "Đang xử lý ảnh trong DSLRBooth…";
                Progress = 0; // giao cho OnPhotoReady điều hướng sang ThankYou
            }
            catch (OperationCanceledException)
            {
                Status = "Đã hủy phiên chụp";
                Progress = 0;
            }
            catch (Exception ex)
            {
                Status = $"Lỗi: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void CancelWorkflow()
        {
            _cts?.Cancel();
        }

        // ======= HELPERS =======
        private async Task CountdownAsync(int totalMs, CancellationToken token)
        {
            // Mô phỏng 3→2→1 bằng 3 nhịp đều nhau
            int steps = 3;
            int perStep = Math.Max(1, totalMs / steps);

            for (int i = steps; i >= 1; i--)
            {
                ThrowIfCanceled();
                Status = $"Đếm ngược… {i}";
                // cập nhật Progress tương ứng (100%, 66%, 33% -> quay về 0 khi xong)
                Progress = (int)Math.Round(i * (100.0 / steps));
                await DelayCancelable(perStep, token);
            }

            // Hoàn tất đếm ngược
            Progress = 100;
            await DelayCancelable(150, token); // nháy 1 nhịp nhỏ
            Progress = 0;
        }

        private async Task DelayCancelable(int ms, CancellationToken token)
        {
            await Task.Delay(ms, token);
        }

        private void ThrowIfCanceled()
        {
            if (_cts?.IsCancellationRequested == true)
                throw new OperationCanceledException();
        }
    }
}