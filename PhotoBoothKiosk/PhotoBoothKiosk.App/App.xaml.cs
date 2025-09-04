using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using PhotoBoothKiosk.Core.Services;
using PhotoBoothKiosk.Core.Utils;

namespace PhotoBoothKiosk.App
{
    public partial class App : Application
    {
        // Cấu hình toàn cục (đọc từ appsettings.json)
        public static AppConfig Config { get; private set; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) Tải cấu hình
            LoadConfig();

            // 2) Kiosk hardening (tuỳ chọn theo cấu hình)
            try
            {
                if (Config.Kiosk.DisableKeys)
                    KeyboardHook.InstallLowLevelHook();
            }
            catch { /* Không chặn toàn app nếu hook lỗi */ }

            // 3) Webhook DSLRBooth (nếu bật)
            try
            {
                if (Config.WebhookServer.Enabled && !string.IsNullOrWhiteSpace(Config.WebhookServer.Prefix))
                    WebhookServer.TryStart(Config.WebhookServer.Prefix);
            }
            catch { /* ignore */ }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { WebhookServer.Stop(); } catch { }
            try { KeyboardHook.UninstallLowLevelHook(); } catch { }
            base.OnExit(e);
        }

        private static void LoadConfig()
        {
            try
            {
                var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(cfgPath))
                {
                    var json = File.ReadAllText(cfgPath);
                    Config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AppConfig();
                }
            }
            catch
            {
                Config = new AppConfig(); // fallback mặc định
            }
        }
    }

    // ===========================
    //  CÁC LỚP ÁNH XẠ CẤU HÌNH
    // ===========================
    public class AppConfig
    {
        public KioskConfig Kiosk { get; set; } = new();
        public DslrBoothConfig DslrBooth { get; set; } = new();
        public PaymentConfig Payment { get; set; } = new();
        public WebhookConfig WebhookServer { get; set; } = new();
        public PricingConfig Pricing { get; set; } = new();
    }

    public class KioskConfig
    {
        public bool FullScreen { get; set; } = true;
        public bool TopMost    { get; set; } = true;
        public bool DisableKeys { get; set; } = true; // chặn Alt+Tab, Alt+F4, WinKey, ...
    }

    public class DslrBoothConfig
    {
        // Mode: "Keyboard" (SendKeys). Có thể mở rộng "HttpApi" nếu dùng API chính thức.
        public string   Mode         { get; set; } = "Keyboard";
        public string   ProcessName  { get; set; } = "dslrBooth";
        public string   OutputFolder { get; set; } = "";
        public st
