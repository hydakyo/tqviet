using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhotoBoothKiosk.Core.Models;

namespace PhotoBoothKiosk.Core.Services
{
    /// <summary>
    /// SessionManager: điều phối luồng chụp ảnh với DSLRBooth.
    /// - TriggerCaptureAsync(): kích hoạt chụp 1 tấm (DSLRBooth lo UI và lưu file).
    /// - Theo dõi thư mục OutputFolder (FileSystemWatcher) và/hoặc nhận webhook để biết khi ảnh đã sẵn sàng.
    /// - Bắn OnPhotoReady(PhotoResult) khi có ảnh mới.
    /// - Quản lý trạng thái phiên qua SessionState (Idle/InSession/Processing).
    /// </summary>
    public sealed class SessionManager : IDisposable
    {
        private readonly DslrBoothController _dslr;
        private readonly string _outputFolder;

        private FileSystemWatcher? _watcher;
        private readonly ConcurrentDictionary<string, DateTime> _recentFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _dupWindow = TimeSpan.FromSeconds(5);
        private SessionState _state = SessionState.Idle;
        private readonly object _gate = new();

        public SessionState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary> Fired khi có ảnh/print mới sẵn sàng (đường dẫn file có thể là ảnh render cuối). </summary>
        public event EventHandler<PhotoResult>? OnPhotoReady;

        /// <summary> Fired khi state thay đổi (Idle/InSession/Processing). </summary>
        public event EventHandler<SessionState>? OnStateChanged;

        public SessionManager(DslrBoothController dslr)
        {
            _dslr = dslr ?? throw new ArgumentNullException(nameof(dslr));
            _outputFolder = dslr.OutputFolder ?? string.Empty;

            TryInitWatcher(_outputFolder);

            // Đăng ký nhận webhook (nếu server đã Start ở App)
            WebhookServer.PhotoSaved += WebhookServer_PhotoSaved;
        }

        /// <summary>
        /// Kích hoạt 1 lần chụp (DSLRBooth tự lo UI/đếm ngược/ghi file theo cấu hình).
        /// ViewModel có thể gọi liên tiếp nhiều lần để chụp nhiều tấm.
        /// </summary>
        public async Task TriggerCaptureAsync(CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (State == SessionState.Idle)
                    State = SessionState.InSession;
            }

            await _dslr.TriggerCaptureAsync(ct).ConfigureAwait(false);

            // Sau khi gửi lệnh chụp, coi như đang Processing cho đến khi ảnh sẵn
            lock (_gate)
            {
                if (State == SessionState.InSession)
                    State = SessionState.Processing;
            }
        }

        private void TryInitWatcher(string outputFolder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
                    return;

                _watcher = new FileSystemWatcher(outputFolder)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                _watcher.Created += OnFileEvent;
                _watcher.Changed += OnFileEvent;
                _watcher.Renamed += OnFileRenamed;
            }
            catch
            {
                // Không ngắt app nếu watcher lỗi; webhook vẫn hoạt động.
                _watcher = null;
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            // Lọc các phần mở rộng ảnh phổ biến do DSLRBooth xuất
            var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png")) return;

            // Debounce: cùng file có thể bắn nhiều sự kiện
            if (IsDuplicate(e.FullPath)) return;

            // Đảm bảo file sẵn sàng đọc (không còn bị lock)
            _ = Task.Run(async () =>
            {
                if (await WaitFileReadyAsync(e.FullPath, TimeSpan.FromSeconds(5)))
                {
                    RaisePhotoReady(e.FullPath);
                }
            });
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileEvent(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath) ?? "", Path.GetFileName(e.FullPath)));
        }

        private void WebhookServer_PhotoSaved(object? sender, PhotoSavedEventArgs e)
        {
            var path = e.FilePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                if (!IsDuplicate(path))
                    RaisePhotoReady(path);
                return;
            }

            // Nếu webhook không có path cụ thể: cố gắng lấy file mới nhất trong outputFolder
            if (!string.IsNullOrWhiteSpace(_outputFolder) && Directory.Exists(_outputFolder))
            {
                try
                {
                    var latest = new DirectoryInfo(_outputFolder)
                        .EnumerateFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => f.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                    f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (latest is not null && !IsDuplicate(latest.FullName))
                        RaisePhotoReady(latest.FullName);
                }
                catch { /* ignore */ }
            }
        }

        private void RaisePhotoReady(string fullPath)
        {
            // Cập nhật trạng thái về Idle (kết thúc phiên) – hoặc bạn có thể tự quản lý phức tạp hơn
            State = SessionState.Idle;

            var result = new PhotoResult
            {
                FilePath = fullPath,
                FileName = Path.GetFileName(fullPath),
                CreatedUtc = DateTime.UtcNow
            };

            try { OnPhotoReady?.Invoke(this, result); } catch { }
        }

        private bool IsDuplicate(string path)
        {
            var now = DateTime.UtcNow;
            var last = _recentFiles.AddOrUpdate(path, now, (_, prev) => now);
            // Nếu khác entry trước đó ít hơn _dupWindow → coi là trùng
            return (now - last) < _dupWindow;
        }

        private static async Task<bool> WaitFileReadyAsync(string path, TimeSpan timeout)
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < timeout)
            {
                try
                {
                    using var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (s.Length > 0) return true;
                }
                catch { /* file có thể đang ghi, thử lại */ }

                await Task.Delay(120);
            }
            return false;
        }

        public void Dispose()
        {
            try
            {
                WebhookServer.PhotoSaved -= WebhookServer_PhotoSaved;
            }
            catch { }

            if (_watcher is not null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= OnFileEvent;
                    _watcher.Changed -= OnFileEvent;
                    _watcher.Renamed -= OnFileRenamed;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }
        }
    }
}