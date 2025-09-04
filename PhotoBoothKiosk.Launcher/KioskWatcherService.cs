using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading;

namespace PhotoBoothKiosk.Launcher
{
    /// <summary>
    /// Windows Service giám sát kiosk app:
    /// - Đảm bảo process đang chạy; nếu không, khởi động lại.
    /// - Hỗ trợ kiểm tra "healthy" qua heartbeat file; nếu staleness vượt ngưỡng -> restart.
    /// - Loại bỏ bội bản (nhiều instance) nếu phát hiện.
    /// 
    /// Cấu hình đọc từ file JSON cạnh service exe: "launcher.settings.json"
    /// {
    ///   "AppExePath": "C:/PhotoBoothKiosk/PhotoBoothKiosk.App.exe",
    ///   "ProcessName": "PhotoBoothKiosk.App",
    ///   "CheckIntervalSeconds": 15,
    ///   "RestartDelaySeconds": 2,
    ///   "HeartbeatFile": "C:/ProgramData/PhotoBoothKiosk/heartbeat.txt",
    ///   "HeartbeatStaleSeconds": 120,
    ///   "KillExtraInstances": true,
    ///   "StartHidden": false
    /// }
    /// 
    /// Ghi chú:
    /// - Nếu thay Shell bằng app kiosk, vẫn nên giữ service để xử lý tình huống app crash/treo.
    /// - Dùng EventLog + file log đơn giản để chẩn đoán.
    /// </summary>
    public class KioskWatcherService : ServiceBase
    {
        private Timer _timer;

        // ----- Cấu hình -----
        private WatcherConfig _cfg;

        // ----- Logging đơn giản -----
        private readonly string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private string LogFile => Path.Combine(_logDir, "launcher.log");
        private const long MaxLog = 2 * 1024 * 1024; // 2MB

        public KioskWatcherService()
        {
            ServiceName = "PhotoBoothKioskWatcher";
            CanPauseAndContinue = false;
            CanShutdown = true;

            Directory.CreateDirectory(_logDir);
            _cfg = LoadConfig();
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        protected override void OnStart(string[] args)
        {
            Log("Service starting...");
            _cfg = LoadConfig(); // reload cấu hình nếu vừa cập nhật

            // Tick ngay + thiết lập chu kỳ
            _timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(Math.Max(5, _cfg.CheckIntervalSeconds)));

            Log($"Service started. Watching: {(_cfg.ProcessName ?? "(null)")}, exe: {(_cfg.AppExePath ?? "(null)")}");
        }

        protected override void OnStop()
        {
            Log("Service stopping...");
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
            Log("Service stopped.");
        }

        protected override void OnShutdown()
        {
            Log("System is shutting down.");
            base.OnShutdown();
        }

        // ========================
        //         CORE
        // ========================
        private void OnTick(object state)
        {
            try
            {
                EnsureProcess();
                CheckHealth();
                if (_cfg.KillExtraInstances) KillDups();
            }
            catch (Exception ex)
            {
                Log($"Tick error: {ex}");
            }
        }

        private void EnsureProcess()
        {
            var p = FindProcess();
            if (p != null && !p.HasExited)
            {
                // Đã chạy
                return;
            }

            // Không thấy → start
            if (string.IsNullOrWhiteSpace(_cfg.AppExePath) || !File.Exists(_cfg.AppExePath))
            {
                Log($"AppExePath không hợp lệ: '{_cfg.AppExePath}'. Bỏ qua start.");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _cfg.AppExePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(_cfg.AppExePath)!,
                };

                // Tùy chọn start ẩn (ít dùng cho kiosk)
                if (_cfg.StartHidden)
                {
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }

                var started = Process.Start(psi);
                Log($"Started kiosk app. PID={(started?.Id.ToString() ?? "null")}");
                Thread.Sleep(TimeSpan.FromSeconds(Math.Max(0, _cfg.RestartDelaySeconds)));
            }
            catch (Exception ex)
            {
                Log($"Start process failed: {ex.Message}");
            }
        }

        private void CheckHealth()
        {
            // Nếu có HeartbeatFile, kiểm tra staleness
            if (!string.IsNullOrWhiteSpace(_cfg.HeartbeatFile))
            {
                var path = _cfg.HeartbeatFile!;
                try
                {
                    if (!File.Exists(path))
                    {
                        // Heartbeat chưa xuất hiện: có thể app chưa khởi tạo xong -> chờ
                        Log("Heartbeat file not found (first runs?).");
                        return;
                    }

                    var last = File.GetLastWriteTimeUtc(path);
                    var age = DateTime.UtcNow - last;

                    if (age.TotalSeconds > Math.Max(30, _cfg.HeartbeatStaleSeconds))
                    {
                        Log($"Heartbeat stale ({(int)age.TotalSeconds}s). Restarting app...");
                        RestartProcessSafe();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Heartbeat check error: {ex.Message}");
                }
            }
        }

        private void KillDups()
        {
            if (string.IsNullOrWhiteSpace(_cfg.ProcessName)) return;

            var nameNoExt = _cfg.ProcessName!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? _cfg.ProcessName[..^4]
                : _cfg.ProcessName;

            var procs = Process.GetProcessesByName(nameNoExt)
                               .Where(p => !p.HasExited)
                               .ToList();

            if (procs.Count <= 1) return;

            // Giữ process có thời gian khởi tạo sớm nhất (coi là "chính"), kill phần còn lại
            var ordered = procs.OrderBy(p =>
            {
                try { return p.StartTime; } catch { return DateTime.MaxValue; }
            }).ToList();

            var keep = ordered.FirstOrDefault();
            foreach (var extra in ordered.Skip(1))
            {
                try
                {
                    Log($"Killing extra instance PID={extra.Id}");
                    extra.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    Log($"Kill extra instance error: {ex.Message}");
                }
            }

            if (keep != null)
                Log($"Kept instance PID={keep.Id}");
        }

        private void RestartProcessSafe()
        {
            try
            {
                var p = FindProcess();
                if (p != null && !p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(8000);
                }
            }
            catch (Exception ex)
            {
                Log($"Kill on restart error: {ex.Message}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(0, _cfg.RestartDelaySeconds)));
            EnsureProcess();
        }

        private Process FindProcess()
        {
            if (string.IsNullOrWhiteSpace(_cfg.ProcessName)) return null;

            var nameNoExt = _cfg.ProcessName!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? _cfg.ProcessName[..^4]
                : _cfg.ProcessName;

            try
            {
                return Process.GetProcessesByName(nameNoExt).FirstOrDefault(p => !p.HasExited);
            }
            catch
            {
                return null;
            }
        }

        // ========================
        //       CONFIG / LOG
        // ========================
        private WatcherConfig LoadConfig()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.settings.json");
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<WatcherConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (cfg != null)
                    {
                        Log($"Loaded config from launcher.settings.json");
                        return Sanitize(cfg);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Load config failed: {ex.Message}");
                }
            }

            // Mặc định hợp lý
            var def = new WatcherConfig
            {
                AppExePath = @"C:\PhotoBoothKiosk\PhotoBoothKiosk.App.exe",
                ProcessName = "PhotoBoothKiosk.App",
                CheckIntervalSeconds = 15,
                RestartDelaySeconds = 2,
                HeartbeatFile = @"C:\ProgramData\PhotoBoothKiosk\heartbeat.txt",
                HeartbeatStaleSeconds = 120,
                KillExtraInstances = true,
                StartHidden = false
            };
            Log("Using default config.");
            return def;
        }

        private static WatcherConfig Sanitize(WatcherConfig c)
        {
            c.CheckIntervalSeconds = Math.Max(5, c.CheckIntervalSeconds);
            c.RestartDelaySeconds = Math.Clamp(c.RestartDelaySeconds, 0, 30);
            c.HeartbeatStaleSeconds = Math.Max(30, c.HeartbeatStaleSeconds);
            return c;
        }

        private void Log(string msg)
        {
            try
            {
                if (!EventLog.SourceExists(ServiceName))
                    EventLog.CreateEventSource(ServiceName, "Application");
                EventLog.WriteEntry(ServiceName, msg);
            }
            catch { /* ignore eventlog errors */ }

            try
            {
                // Rollover đơn giản
                Directory.CreateDirectory(_logDir);
                if (File.Exists(LogFile))
                {
                    var fi = new FileInfo(LogFile);
                    if (fi.Length > MaxLog)
                    {
                        var bak = LogFile + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        File.Move(LogFile, bak, overwrite: true);
                    }
                }
                File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch { /* ignore file log errors */ }
        }
    }

    public class WatcherConfig
    {
        public string AppExePath { get; set; }
        public string ProcessName { get; set; }
        public int CheckIntervalSeconds { get; set; } = 15;
        public int RestartDelaySeconds { get; set; } = 2;
        public string HeartbeatFile { get; set; }
        public int HeartbeatStaleSeconds { get; set; } = 120;
        public bool KillExtraInstances { get; set; } = true;
        public bool StartHidden { get; set; } = false;
    }
}