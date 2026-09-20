using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MultiShot
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        internal const int WM_HOTKEY = 0x0312;
        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint VK_X = 0x58;
        internal const uint VK_Z = 0x5A;
        internal const uint VK_RETURN = 0x0D;
        internal const uint GW_HWNDNEXT = 2;
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        internal const int DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }
    }

    internal sealed class HotkeyDefinition
    {
        internal uint Modifiers;
        internal Keys Key;

        internal HotkeyDefinition(uint modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        internal HotkeyDefinition Clone()
        {
            return new HotkeyDefinition(Modifiers, Key);
        }

        internal string ToDisplayString()
        {
            List<string> parts = new List<string>();
            if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(KeyToDisplayName(Key));
            return string.Join("+", parts.ToArray());
        }

        internal string ToConfigString()
        {
            return ToDisplayString();
        }

        internal bool SameAs(HotkeyDefinition other)
        {
            return other != null && other.Modifiers == Modifiers && other.Key == Key;
        }

        internal static HotkeyDefinition CaptureDefault()
        {
            return new HotkeyDefinition(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, Keys.X);
        }

        internal static HotkeyDefinition UndoDefault()
        {
            return new HotkeyDefinition(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, Keys.Z);
        }

        internal static HotkeyDefinition FinishDefault()
        {
            return new HotkeyDefinition(NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, Keys.Enter);
        }

        internal static bool TryParse(string text, out HotkeyDefinition result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            uint modifiers = 0;
            Keys key = Keys.None;
            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= NativeMethods.MOD_CONTROL;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= NativeMethods.MOD_ALT;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= NativeMethods.MOD_SHIFT;
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    modifiers |= NativeMethods.MOD_WIN;
                else
                {
                    Keys parsed;
                    if (!Enum.TryParse<Keys>(NormalizeKeyName(part), true, out parsed))
                        return false;
                    key = parsed;
                }
            }

            if (key == Keys.None || IsModifierKey(key)) return false;
            result = new HotkeyDefinition(modifiers, key);
            return true;
        }

        internal static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        private static string NormalizeKeyName(string value)
        {
            if (value.Equals("Enter", StringComparison.OrdinalIgnoreCase)) return "Enter";
            if (value.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return "Escape";
            if (value.Equals("Space", StringComparison.OrdinalIgnoreCase)) return "Space";
            if (value.Length == 1 && char.IsDigit(value[0])) return "D" + value;
            return value;
        }

        private static string KeyToDisplayName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((int)key - (int)Keys.D0).ToString(CultureInfo.InvariantCulture);
            if (key == Keys.Escape) return "Esc";
            if (key == Keys.Return) return "Enter";
            return key.ToString();
        }
    }

    internal sealed class AppSettings
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiShot");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.ini");

        internal string Language = "auto";
        internal HotkeyDefinition Capture = HotkeyDefinition.CaptureDefault();
        internal HotkeyDefinition Undo = HotkeyDefinition.UndoDefault();
        internal HotkeyDefinition Finish = HotkeyDefinition.FinishDefault();

        internal static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsFile)) return settings;
                foreach (string rawLine in File.ReadAllLines(SettingsFile))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();

                    if (key.Equals("language", StringComparison.OrdinalIgnoreCase))
                    {
                        if (value == "auto" || value == "zh-CN" || value == "en")
                            settings.Language = value;
                    }
                    else if (key.Equals("capture", StringComparison.OrdinalIgnoreCase))
                    {
                        HotkeyDefinition hotkey;
                        if (HotkeyDefinition.TryParse(value, out hotkey)) settings.Capture = hotkey;
                    }
                    else if (key.Equals("undo", StringComparison.OrdinalIgnoreCase))
                    {
                        HotkeyDefinition hotkey;
                        if (HotkeyDefinition.TryParse(value, out hotkey)) settings.Undo = hotkey;
                    }
                    else if (key.Equals("finish", StringComparison.OrdinalIgnoreCase))
                    {
                        HotkeyDefinition hotkey;
                        if (HotkeyDefinition.TryParse(value, out hotkey)) settings.Finish = hotkey;
                    }
                }
            }
            catch { }

            if (settings.Capture.Modifiers == 0 || settings.Undo.Modifiers == 0 || settings.Finish.Modifiers == 0 ||
                settings.Capture.SameAs(settings.Undo) || settings.Capture.SameAs(settings.Finish) ||
                settings.Undo.SameAs(settings.Finish))
            {
                settings.Capture = HotkeyDefinition.CaptureDefault();
                settings.Undo = HotkeyDefinition.UndoDefault();
                settings.Finish = HotkeyDefinition.FinishDefault();
            }
            return settings;
        }

        internal void Save()
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllLines(SettingsFile, new string[]
            {
                "# MultiShot settings",
                "language=" + Language,
                "capture=" + Capture.ToConfigString(),
                "undo=" + Undo.ToConfigString(),
                "finish=" + Finish.ToConfigString()
            });
        }
    }

    internal static class I18n
    {
        private static bool useChinese = true;

        internal static void SetLanguage(string language)
        {
            if (language == "zh-CN") useChinese = true;
            else if (language == "en") useChinese = false;
            else useChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
        }

        internal static string T(string key, params object[] args)
        {
            string text = useChinese ? Zh(key) : En(key);
            if (args != null && args.Length > 0)
                return string.Format(CultureInfo.CurrentCulture, text, args);
            return text;
        }

        private static string Zh(string key)
        {
            switch (key)
            {
                case "AlreadyRunning": return "MultiShot 已经在运行。\n\n可在右下角托盘找到它，或直接使用 {0} 截图。";
                case "NoShots": return "当前没有待处理截图";
                case "Capture": return "截图";
                case "Undo": return "撤销上一张";
                case "Finish": return "完成并复制";
                case "Cancel": return "取消整组";
                case "Settings": return "设置";
                case "ShowController": return "显示控制条";
                case "Exit": return "退出";
                case "TrayIdle": return "MultiShot - 连续截图剪贴板";
                case "TrayPending": return "MultiShot - {0} 张待完成";
                case "StillRunningTitle": return "MultiShot 仍在运行";
                case "StillRunningBody": return "按 {0} 可继续截图。";
                case "CaptureFailed": return "截图失败：\n{0}";
                case 