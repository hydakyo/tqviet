using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhotoBoothKiosk.Core.Services
{
    /// <summary>
    /// Điều khiển DSLRBooth: đảm bảo chạy, focus, và kích hoạt chụp.
    /// - Mode "Keyboard": dùng SendKeys để phát lệnh chụp (ví dụ {F4}).
    /// - Mode "HttpApi": chừa sẵn stub để gọi API (nếu bạn bật API trong DSLRBooth).
    /// </summary>
    public class DslrBoothController
    {
        public string ProcessName { get; }
        public string[] KeyboardSequence { get; }
        public string OutputFolder { get; }
        public string Mode { get; }

        private readonly HttpClient? _http;
        private Process? _cached;

        public DslrBoothController(string processName, string[] keyboardSequence, string outputFolder, string mode = "Keyboard")
        {
            ProcessName = string.IsNullOrWhiteSpace(processName) ? "dslrBooth" : processName;
            KeyboardSequence = keyboardSequence is { Length: > 0 } ? keyboardSequence : new[] { "{F4}" };
            OutputFolder = outputFolder ?? string.Empty;
            Mode = string.IsNullOrWhiteSpace(mode) ? "Keyboard" : mode;

            if (Mode.Equals("HttpApi", StringComparison.OrdinalIgnoreCase))
            {
                _http = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
            }
        }

        /// <summary>
        /// Kích hoạt một lần chụp: đảm bảo app đang chạy, focus, rồi gửi lệnh (phím/API).
        /// </summary>
        public async Task TriggerCaptureAsync(CancellationToken ct = default)
        {
            // 1) Đảm bảo tiến trình đang chạy
            var proc = await EnsureRunningAsync(ct).ConfigureAwait(false);

            // 2) Tùy mode: gửi lệnh
            if (Mode.Equals("Keyboard", StringComparison.OrdinalIgnoreCase))
            {
                // Đưa cửa sổ lên trước để nhận phím (best effort)
                TryFocus(proc);

                // Gửi lần lượt các key trong KeyboardSequence
                foreach (var token in KeyboardSequence)
                {
                    ct.ThrowIfCancellationRequested();
                    SendKeysSendWaitSafe(token);
                    await Task.Delay(60, ct).ConfigureAwait(false);
                }
            }
            else if (Mode.Equals("HttpApi", StringComparison.OrdinalIgnoreCase))
            {
                // Stub minh họa: bạn chỉnh URL, header, auth theo cấu hình của DSLRBooth nếu có API chính thức
                // Ví dụ: POST http://localhost:8080/api/start-capture
                if (_http is not null)
                {
                    var url = "http://localhost:8080/api/start-capture"; // TODO: đưa vào appsettings nếu dùng API thật
                    using var req = new HttpRequestMessage(HttpMethod.Post, url);
                    using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
                    res.EnsureSuccessStatusCode();
                }
                else
                {
                    throw new InvalidOperationException("HttpApi mode enabled nhưng HttpClient chưa được khởi tạo.");
                }
            }
            else
            {
                throw new NotSupportedException($"DslrBoothController Mode không hỗ trợ: {Mode}");
            }
        }

        /// <summary>
        /// Đảm bảo tiến trình DSLRBooth đang chạy. Nếu chưa, cố gắng khởi động.
        /// </summary>
        public async Task<Process> EnsureRunningAsync(CancellationToken ct = default)
        {
            var p = GetProcess();
            if (p is not null) return p;

            // Thử khởi động bằng tên tiến trình (cần có trong PATH hoặc shell đăng ký .lnk)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ProcessName, // ví dụ "dslrBooth"
                    UseShellExecute = true,
                    // Bạn có thể set WorkingDirectory/Arguments nếu cần
                };
                _cached = Process.Start(psi);
            }
            catch (Exception ex)
            {
                DebugWrite($"Không thể start process '{ProcessName}': {ex.Message}");
                // Thử thêm .exe
                try
                {
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ProcessName : $"{ProcessName}.exe",
                        UseShellExecute = true
                    };
                    _cached = Process.Start(psi2);
                }
                catch (Exception ex2)
                {
                    DebugWrite($"Start với .exe thất bại: {ex2.Message}");
                    throw; // không thể khởi động
                }
            }

            // Chờ main window hiện (best effort)
            await WaitForMainWindowAsync(_cached!, TimeSpan.FromSeconds(8), ct).ConfigureAwait(false);
            return _cached!;
        }

        /// <summary>
        /// Thử đưa cửa sổ DSLRBooth lên trước để nhận phím gửi bằng SendKeys.
        /// </summary>
        public bool TryFocus(Process? proc = null)
        {
            proc ??= GetProcess();
            if (proc is null) return false;

            try
            {
                var h = proc.MainWindowHandle;
                if (h == IntPtr.Zero)
                {
                    // Có thể cửa sổ chưa kịp tạo; thử đợi ngắn
                    for (int i = 0; i < 10 && h == IntPtr.Zero; i++)
                    {
                        Thread.Sleep(100);
                        proc.Refresh();
                        h = proc.MainWindowHandle;
                    }
                }

                if (h != IntPtr.Zero)
                {
                    ShowWindow(h, SW_RESTORE);
                    return SetForegroundWindow(h);
                }
            }
            catch (Exception ex)
            {
                DebugWrite($"TryFocus lỗi: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Lấy process DSLRBooth đang chạy (cache + lookup).
        /// </summary>
        private Process? GetProcess()
        {
            try
            {
                if (_cached is { HasExited: false })
                    return _cached;

                var nameNoExt = ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? ProcessName[..^4]
                    : ProcessName;

                var procs = Process.GetProcessesByName(nameNoExt);
                _cached = procs.FirstOrDefault(p => !p.HasExited);
                return _cached;
            }
            catch { return null; }
        }

        private static async Task WaitForMainWindowAsync(Process p, TimeSpan timeout, CancellationToken ct)
        {
            var start = DateTime.UtcNow;
            while (p.MainWindowHandle == IntPtr.Zero && !p.HasExited)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow - start > timeout) break;
                await Task.Delay(150, ct).ConfigureAwait(false);
                try { p.Refresh(); } catch { }
            }
        }

        private static void SendKeysSendWaitSafe(string keys)
        {
            try
            {
                // SendKeys yêu cầu STA; thông thường WPF/WinForms thread UI đã là STA.
                SendKeys.SendWait(keys);
            }
            catch (Exception ex)
            {
                DebugWrite($"SendKeys '{keys}' lỗi: {ex.Message}");
            }
        }

        private static void DebugWrite(string msg)
        {
            try { Debug.WriteLine(msg); } catch { }
            try { Console.WriteLine(msg); } catch { }
        }

        #region Win32 P/Invoke
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;
        #endregion
    }
}