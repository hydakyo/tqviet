using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoBoothKiosk.Core.Services
{
    /// <summary>
    /// WebhookServer: HttpListener đơn giản cho DSLRBooth webhook.
    /// - Start bằng WebhookServer.TryStart("http://localhost:7071/");
    /// - Lắng /dslrbooth/photo-saved (GET/POST). Trả 200 nếu nhận OK.
    /// - Khi nhận dữ liệu, phát event PhotoSaved để tầng trên xử lý.
    /// </summary>
    public static class WebhookServer
    {
        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;
        private static Task? _worker;

        public static bool IsRunning => _listener is not null && _listener.IsListening;

        /// <summary> Raised khi DSLrBooth báo ảnh đã lưu. </summary>
        public static event EventHandler<PhotoSavedEventArgs>? PhotoSaved;

        /// <summary>
        /// Khởi động server trên prefix (vd: "http://localhost:7071/").
        /// Nếu server đã chạy, bỏ qua.
        /// </summary>
        public static bool TryStart(string prefix)
        {
            try
            {
                if (IsRunning) return true;

                _listener = new HttpListener();
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(NormalizePrefix(prefix));
                _listener.Start();

                _cts = new CancellationTokenSource();
                _worker = Task.Run(() => AcceptLoopAsync(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Log($"WebhookServer.TryStart error: {ex.Message}");
                SafeStop();
                return false;
            }
        }

        /// <summary>Dừng server.</summary>
        public static void Stop() => SafeStop();

        // ===========================
        //            Core
        // ===========================
        private static async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _listener is { IsListening: true })
                {
                    HttpListenerContext? ctx = null;

                    try
                    {
                        ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    }
                    catch when (token.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        Log($"GetContextAsync error: {ex.Message}");
                        continue;
                    }

                    _ = Task.Run(() => HandleRequestAsync(ctx, token), token);
                }
            }
            catch (Exception ex)
            {
                Log($"AcceptLoopAsync fatal: {ex.Message}");
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken token)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            // CORS tối thiểu
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";

            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                await WriteStringAsync(res, 204, string.Empty).ConfigureAwait(false);
                return;
            }

            var path = (req.Url?.AbsolutePath ?? "").TrimEnd('/').ToLowerInvariant();

            try
            {
                if (path == "/dslrbooth/photo-saved")
                {
                    await HandlePhotoSavedAsync(req, res, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteStringAsync(res, 404, "Not Found").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log($"HandleRequestAsync error: {ex.Message}");
                try { await WriteStringAsync(res, 500, "Internal Server Error").ConfigureAwait(false); } catch { }
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private static async Task HandlePhotoSavedAsync(HttpListenerRequest req, HttpListenerResponse res, CancellationToken token)
        {
            string raw = string.Empty;
            string? filePath = null;
            string? fileName = null;

            // Nhận JSON body (POST) hoặc query param (GET)
            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding);
                raw = await reader.ReadToEndAsync().ConfigureAwait(false);

                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                    var root = doc.RootElement;

                    // linh hoạt theo payload của DSLRBooth (tuỳ cấu hình)
                    filePath = TryGetString(root, "path") ?? TryGetString(root, "file") ?? TryGetString(root, "filename");
                    fileName = TryGetString(root, "filename") ?? TryGetString(root, "name");
                }
                catch (Exception jex)
                {
                    Log($"JSON parse error: {jex.Message}");
                }
            }
            else // GET
            {
                var q = req.Url?.Query ?? "";
                var col = System.Web.HttpUtility.ParseQueryString(q);
                filePath = col.Get("path") ?? col.Get("file") ?? col.Get("filename");
                fileName = col.Get("filename") ?? col.Get("name");
            }

            // Bắn event cho tầng trên (SessionManager/VM) xử lý
            try
            {
                PhotoSaved?.Invoke(null, new PhotoSavedEventArgs
                {
                    FilePath = filePath ?? string.Empty,
                    FileName = fileName ?? string.Empty,
                    RawJson  = raw
                });
            }
            catch (Exception ex)
            {
                Log($"PhotoSaved subscribers error: {ex.Message}");
            }

            await WriteStringAsync(res, 200, "OK").ConfigureAwait(false);
        }

        private static string NormalizePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return "http://localhost:7071/";
            if (!prefix.EndsWith("/")) prefix += "/";
            return prefix;
        }

        private static string? TryGetString(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }

        private static async Task WriteStringAsync(HttpListenerResponse res, int statusCode, string content)
        {
            res.StatusCode = statusCode;
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            res.ContentType = "text/plain; charset=utf-8";
            res.ContentEncoding = Encoding.UTF8;
            res.ContentLength64 = bytes.LongLength;
            await res.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }

        private static void SafeStop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            _cts = null;
            _worker = null;
        }

        private static void Log(string msg)
        {
            try { System.Diagnostics.Debug.WriteLine(msg); } catch { }
            try { Console.WriteLine(msg); } catch { }
        }
    }

    public sealed class PhotoSavedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RawJson  { get; set; } = string.Empty;
    }
}