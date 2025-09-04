using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhotoBoothKiosk.Core.Utils
{
    /// <summary>
    /// Low-level keyboard hook để chặn hotkey hệ thống trong môi trường kiosk.
    /// - Gọi InstallLowLevelHook() khi khởi động app; UninstallLowLevelHook() khi thoát.
    /// - Dùng SetExitShortcutFromString("Ctrl+Shift+Q") để đặt phím tắt thoát.
    /// - Lắng sự kiện ExitShortcutPressed để thực thi thoát app ở tầng UI.
    /// </summary>
    public static class KeyboardHook
    {
        // =======================
        //   Public API / Events
        // =======================
        public static event EventHandler? ExitShortcutPressed;

        public static void InstallLowLevelHook()
        {
            if (_hookHandle != IntPtr.Zero) return;
            _proc = HookCallback;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(IntPtr.Zero), 0);
        }

        public static void UninstallLowLevelHook()
        {
            try
            {
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Đặt phím tắt thoát dạng chuỗi, ví dụ: "Ctrl+Shift+Q", "Alt+F12", "Ctrl+Alt+E", "Win+End".
        /// </summary>
        public static void SetExitShortcutFromString(string? shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                _exitModCtrl = _exitModAlt = _exitModShift = _exitModWin = false;
                _exitKey = Keys.None;
                return;
            }

            bool ctrl = false, alt = false, shift = false, win = false;
            Keys key = Keys.None;

            var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                var p = raw.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    ctrl = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    alt = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    shift = true;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    win = true;
                else
                {
                    // Phần còn lại coi là phím chính
                    if (Enum.TryParse<Keys>(p, true, out var parsed))
                        key = parsed;
                }
            }

            _exitModCtrl = ctrl;
            _exitModAlt = alt;
            _exitModShift = shift;
            _exitModWin = win;
            _exitKey = key;
        }

        // =======================
        //        Internals
        // =======================
        private static IntPtr _hookHandle = IntPtr.Zero;
        private static LowLevelKeyboardProc? _proc;

        // Trạng thái modifier hiện tại
        private static bool _ctrlDown, _altDown, _shiftDown, _winDown;

        // Cấu hình phím tắt thoát
        private static bool _exitModCtrl, _exitModAlt, _exitModShift, _exitModWin;
        private static Keys _exitKey = Keys.None;

        // Danh sách phím/combination cần chặn
        private static bool ShouldBlockKey(int vkCode, bool isKeyDown)
        {
            if (!isKeyDown) return false; // chỉ xử lý ở KeyDown

            var key = (Keys)vkCode;

            // Chặn phím Windows (trái/phải)
            if (key == Keys.LWin || key == Keys.RWin) return true;

            // Alt + Tab
            if (_altDown && key == Keys.Tab) return true;

            // Alt + F4
            if (_altDown && key == Keys.F4) return true;

            // Ctrl + Esc
            if (_ctrlDown && key == Keys.Escape) return true;

            // Alt + Esc
            if (_altDown && key == Keys.Escape) return true;

            // Alt + Space
            if (_altDown && key == Keys.Space) return true;

            // Win + L (khóa màn hình) - best effort
            if (_winDown && key == Keys.L) return true;

            // Win + D (show desktop) - optional
            if (_winDown && key == Keys.D) return true;

            // Win + Tab (task switcher)
            if (_winDown && key == Keys.Tab) return true;

            return false;
        }

        private static bool IsExitShortcut(int vkCode, bool isKeyDown)
        {
            if (!isKeyDown) return false;
            if (_exitKey == Keys.None) return false;

            bool modOk =
                (_exitModCtrl ? _ctrlDown : true) &&
                (_exitModAlt ? _altDown : true) &&
                (_exitModShift ? _shiftDown : true) &&
                (_exitModWin ? _winDown : true);

            // Nếu shortcut có yêu cầu một modifier mà hiện không nhấn -> false
            if (_exitModCtrl && !_ctrlDown) return false;
            if (_exitModAlt && !_altDown) return false;
            if (_exitModShift && !_shiftDown) return false;
            if (_exitModWin && !_winDown) return false;

            return modOk && (Keys)vkCode == _exitKey;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int msg = wParam.ToInt32();
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var vkCode = data.vkCode;

                    bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    bool isUp   = msg == WM_KEYUP   || msg == WM_SYSKEYUP;

                    // Cập nhật trạng thái modifier
                    UpdateModifiers(vkCode, isDown, isUp);

                    // Exit shortcut?
                    if (IsExitShortcut(vkCode, isDown))
                    {
                        // Phát sự kiện để UI thoát; swallow sự kiện để không lộ phím tắt
                        SafeRaiseExit();
                        return (IntPtr)1;
                    }

                    // Chặn hotkey hệ thống?
                    if (ShouldBlockKey(vkCode, isDown))
                    {
                        return (IntPtr)1; // swallow
                    }
                }
            }
            catch (Exception ex)
            {
                DebugWrite($"Keyboard hook error: {ex.Message}");
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private static void UpdateModifiers(int vkCode, bool isDown, bool isUp)
        {
            var k = (Keys)vkCode;
            switch (k)
            {
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.ControlKey:
                    _ctrlDown = isDown ? true : (isUp ? false : _ctrlDown);
                    break;

                case Keys.LMenu:   // Left Alt
                case Keys.RMenu:   // Right Alt
                case Keys.Menu:    // Alt
                    _altDown = isDown ? true : (isUp ? false : _altDown);
                    break;

                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ShiftKey:
                    _shiftDown = isDown ? true : (isUp ? false : _shiftDown);
                    break;

                case Keys.LWin:
                case Keys.RWin:
                    _winDown = isDown ? true : (isUp ? false : _winDown);
                    break;
            }
        }

        private static void SafeRaiseExit()
        {
            try { ExitShortcutPressed?.Invoke(null, EventArgs.Empty); } catch { }
        }

        private static void DebugWrite(string msg)
        {
            try { Debug.WriteLine(msg); } catch { }
            try { Console.WriteLine(msg); } catch { }
        }

        // =======================
        //      Win32 P/Invoke
        // =======================
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_KEYUP      = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP   = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);
    }
}