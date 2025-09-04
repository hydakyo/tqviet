using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.Core.Utils
{
    /// <summary>
    /// Logger nhẹ cho kiosk:
    /// - Thread-safe, hàng đợi nội bộ + nền ghi file.
    /// - Ghi Console + File (UTF8).
    /// - Rolling theo kích thước (mặc định 5MB, giữ N file).
    /// - Mức log: Trace/Debug/Info/Warn/Error/Fatal.
    /// </summary>
    public static class Logger
    {
        private static readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
        private static CancellationTokenSource? _cts;
        private static Task? _worker;

        private static readonly object _fileGate = new();
        private static string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static string _logFileBase = "kiosk.log";
        private static string _currentPath = "";
        private static long _rollSizeBytes = 5 * 1024 * 1024; // 5MB
        private static int _maxRollFiles = 5;

        private static LogLevel _minLevel = LogLevel.Info;
        private static bool _initialized;

        public static void Initialize(
            string? logDirectory = null,
            string? logFileBaseName = null,
            long rollSizeBytes = 5 * 1024 * 1024,
            int maxRollFiles = 5,
            LogLevel minLevel = LogLevel.Info)
        {
            if (_initialized) return;

            _logDir = string.IsNullOrWhiteSpace(logDirectory) ? _logDir : logDirectory!;
            _logFileBase = string.IsNullOrWhiteSpace(logFileBaseName) ? _logFileBase : logFileBaseName!;
            _rollSizeBytes = Math.Max(256 * 1024, rollSizeBytes);
            _maxRollFiles = Math.Max(1, maxRollFiles);
            _minLevel = minLevel;

            Directory.CreateDirectory(_logDir);
            _currentPath = Path.Combine(_logDir, _logFileBase);

            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => WriterLoopAsync(_cts.Token));

            _initialized = true;
            Info("Logger initialized");
        }

        public static void Shutdown()
        {
            try
            {
                _cts?.Cancel();
                _queue.CompleteAdding();
            }
            catch { /* ignore */ }

            try { _worker?.Wait(1500); } catch { }

            _cts = null;
            _worker = null;
            _initialized = false;
        }

        public static void SetMinLevel(LogLevel level) => _minLevel = level;

        // ------------- API tiện dụng -------------
        public static void Trace(string msg) => Log(LogLevel.Trace, msg);
        public static void Debug(string msg) => Log(LogLevel.Debug, msg);
        public static void Info (string msg) => Log(LogLevel.Info,  msg);
        public static void Warn (string msg) => Log(LogLevel.Warn,  msg);
        public static void Error(string msg, Exception? ex = null) => Log(LogLevel.Error, msg, ex);
        public static void Fatal(string msg, Exception? ex = null) => Log(LogLevel.Fatal, msg, ex);

        public static void Log(LogLevel level, string message, Exception? ex = null)
        {
            if (level < _minLevel) return;

            var now = DateTimeOffset.Now;
            var line = FormatLine(now, level, message, ex);

            // Console (best-effort)
            try
            {
                Console.WriteLine(line);
                if (ex != null) Console.WriteLine(ex);
            } catch { /* ignore */ }

            // Debug output
            try
            {
                Debug.WriteLine(line);
                if (ex != null) Debug.WriteLine(ex);
            } catch { /* ignore */ }

            // Enqueue file write
            if (!_initialized) Initialize(); // lazy init
            try { _queue.Add(line); } catch { /* ignore on shutdown */ }
            if (ex != null)
            {
                try { _queue.Add(ex.ToString()); } catch { }
            }
        }

        // ------------- Nội bộ -------------
        private static async Task WriterLoopAsync(CancellationToken token)
        {
            FileStream? stream = null;
            StreamWriter? writer = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = _queue.Take(token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (InvalidOperationException) { break; } // CompleteAdding
                    catch { continue; }

                    try
                    {
                        (stream, writer) = EnsureFile(stream, writer);
                        await writer!.WriteLineAsync(line).ConfigureAwait(false);
                        await writer!.FlushAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Nếu lỗi tệp, thử đóng & mở lại ở vòng sau
                        SafeClose(ref writer, ref stream);
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                SafeClose(ref writer, ref stream);
            }
        }

        private static (FileStream, StreamWriter) EnsureFile(FileStream? fs, StreamWriter? sw)
        {
            lock (_fileGate)
            {
                // Mở file nếu cần
                if (fs == null || sw == null)
                {
                    fs = new FileStream(_currentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = false };
                    return (fs, sw);
                }

                // Kiểm tra rolling
                if (fs.Length >= _rollSizeBytes)
                {
                    SafeClose(ref sw, ref fs);
                    RollFiles(); // đổi tên file cũ
                    fs = new FileStream(_currentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    sw = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = false };
                }
                return (fs, sw);
            }
        }

        private static void RollFiles()
        {
            try
            {
                // Xóa file cũ nhất nếu vượt quá số lượng
                var oldest = Path.Combine(_logDir, $"{_logFileBase}.{_maxRollFiles}.log");
                if (File.Exists(oldest)) File.Delete(oldest);

                // Dịch đuôi .N -> .N+1
                for (int i = _maxRollFiles - 1; i >= 1; i--)
                {
                    var src = Path.Combine(_logDir, $"{_logFileBase}.{i}.log");
                    var dst = Path.Combine(_logDir, $"{_logFileBase}.{i + 1}.log");
                    if (File.Exists(src))
                    {
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }

                // Đổi file hiện tại → .1.log
                var first = Path.Combine(_logDir, $"{_logFileBase}.1.log");
                if (File.Exists(first)) File.Delete(first);
                if (File.Exists(_currentPath)) File.Move(_currentPath, first);
            }
            catch
            {
                // Rolling lỗi: bỏ qua để không gián đoạn ghi log.
            }
        }

        private static void SafeClose(ref StreamWriter? sw, ref FileStream? fs)
        {
            try { sw?.Flush(); } catch { }
            try { sw?.Dispose(); } catch { }
            try { fs?.Dispose(); } catch { }
            sw = null; fs = null;
        }

        private static string FormatLine(DateTimeOffset ts, LogLevel level, string message, Exception? ex)
        {
            // 2025-09-04 10:15:33.123 +07:00 [INFO] Message
            var sb = new StringBuilder(256);
            sb.Append(ts.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            sb.Append(' ');
            sb.Append('[').Append(level.ToString().ToUpperInvariant()).Append("] ");
            sb.Append(message ?? string.Empty);
            if (ex != null)
            {
                sb.Append(" | EX: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            }
            return sb.ToString();
        }
    }

    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info  = 2,
        Warn  = 3,
        Error = 4,
        Fatal = 5
    }
}