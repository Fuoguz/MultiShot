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
                case "NothingToUndo": return "没有可以撤销的截图。";
                case "UndoRemaining": return "已撤销上一张，当前还有 {0} 张";
                case "UndoEmpty": return "已撤销，当前没有截图";
                case "NeedShot": return "还没有截图，先截一张。";
                case "ClipboardUnavailable": return "无法访问 Windows 剪贴板。";
                case "CopiedStatus": return "当前队列：0 张 · 已复制 {0} 张到剪贴板";
                case "CopiedTitle": return "已复制 {0} 张截图";
                case "CopiedBody": return "支持多文件粘贴的软件会一次收到整组截图。";
                case "ClipboardFailed": return "写入剪贴板失败：\n{0}";
                case "Cancelled": return "已取消这组截图。";
                case "Capturing": return "连续截图中：已截 {0} 张";
                case "Hint": return "{0} 截图 · {1} 撤销 · {2} 完成";
                case "HintPartial": return "部分全局快捷键被占用；可在设置中修改，或直接点按钮使用。";
                case "Toast": return "已截 {0} 张   ·   {1} 完成";
                case "OverlayInstruction": return "单击：截取当前窗口    拖拽：自由框选    右键 / Esc：取消";
                case "OverlayCount": return "已截  {0} 张";
                case "ModeManual": return "自由框选";
                case "ModeWindow": return "窗口";
                case "SettingsTitle": return "MultiShot 设置";
                case "Language": return "语言";
                case "LanguageAuto": return "跟随系统 / System default";
                case "LanguageZh": return "简体中文";
                case "LanguageEn": return "English";
                case "CaptureHotkey": return "截图快捷键";
                case "UndoHotkey": return "撤销快捷键";
                case "FinishHotkey": return "完成快捷键";
                case "HotkeyHelp": return "点击输入框后直接按新的组合键。建议至少包含 Ctrl / Alt / Shift。";
                case "Save": return "保存";
                case "ResetDefaults": return "恢复默认";
                case "Close": return "取消";
                case "ModifierRequired": return "快捷键需要至少包含 Ctrl、Alt、Shift 或 Win 中的一个修饰键。";
                case "DuplicateHotkeys": return "三组快捷键不能相同。";
                case "HotkeyConflict": return "无法注册快捷键 {0}。它可能已被其他程序占用。\n\n设置未保存，原快捷键仍然有效。";
                case "SettingsSaved": return "设置已保存";
                case "SettingsSavedBody": return "语言和快捷键已立即生效。";
                default: return key;
            }
        }

        private static string En(string key)
        {
            switch (key)
            {
                case "AlreadyRunning": return "MultiShot is already running.\n\nFind it in the system tray, or press {0} to capture.";
                case "NoShots": return "No pending screenshots";
                case "Capture": return "Capture";
                case "Undo": return "Undo Last";
                case "Finish": return "Finish & Copy";
                case "Cancel": return "Cancel Group";
                case "Settings": return "Settings";
                case "ShowController": return "Show Controller";
                case "Exit": return "Exit";
                case "TrayIdle": return "MultiShot - batch screenshot clipboard";
                case "TrayPending": return "MultiShot - {0} pending";
                case "StillRunningTitle": return "MultiShot is still running";
                case "StillRunningBody": return "Press {0} to keep capturing.";
                case "CaptureFailed": return "Capture failed:\n{0}";
                case "NothingToUndo": return "There is no screenshot to undo.";
                case "UndoRemaining": return "Last screenshot removed. {0} remaining.";
                case "UndoEmpty": return "Last screenshot removed. Queue is empty.";
                case "NeedShot": return "No screenshots yet. Capture one first.";
                case "ClipboardUnavailable": return "Windows Clipboard is unavailable.";
                case "CopiedStatus": return "Queue: 0 · Copied {0} screenshots to Clipboard";
                case "CopiedTitle": return "Copied {0} screenshots";
                case "CopiedBody": return "Apps that support multi-file paste will receive the whole group at once.";
                case "ClipboardFailed": return "Failed to write to Clipboard:\n{0}";
                case "Cancelled": return "Screenshot group cancelled.";
                case "Capturing": return "Batch capture: {0} screenshots";
                case "Hint": return "{0} Capture · {1} Undo · {2} Finish";
                case "HintPartial": return "Some global hotkeys are unavailable. Change them in Settings or use the buttons.";
                case "Toast": return "Captured {0}   ·   {1} to finish";
                case "OverlayInstruction": return "Click: capture window    Drag: free region    Right click / Esc: cancel";
                case "OverlayCount": return "Captured  {0}";
                case "ModeManual": return "Free region";
                case "ModeWindow": return "Window";
                case "SettingsTitle": return "MultiShot Settings";
                case "Language": return "Language";
                case "LanguageAuto": return "System default / 跟随系统";
                case "LanguageZh": return "简体中文";
                case "LanguageEn": return "English";
                case "CaptureHotkey": return "Capture hotkey";
                case "UndoHotkey": return "Undo hotkey";
                case "FinishHotkey": return "Finish hotkey";
                case "HotkeyHelp": return "Focus a field and press the new key combination. Using Ctrl / Alt / Shift is recommended.";
                case "Save": return "Save";
                case "ResetDefaults": return "Reset Defaults";
                case "Close": return "Cancel";
                case "ModifierRequired": return "A hotkey must include at least one modifier: Ctrl, Alt, Shift, or Win.";
                case "DuplicateHotkeys": return "The three hotkeys must be different.";
                case "HotkeyConflict": return "Could not register {0}. Another app may already be using it.\n\nNothing was saved and your previous hotkeys are still active.";
                case "SettingsSaved": return "Settings saved";
                case "SettingsSavedBody": return "Language and hotkeys are active immediately.";
                default: return key;
            }
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox languageCombo;
        private readonly TextBox captureBox;
        private readonly TextBox undoBox;
        private readonly TextBox finishBox;
        private readonly Label helpLabel;
        private readonly Button saveButton;
        private readonly Button resetButton;
        private readonly Button cancelButton;

        internal string SelectedLanguage { get; private set; }
        internal HotkeyDefinition CaptureHotkey { get; private set; }
        internal HotkeyDefinition UndoHotkey { get; private set; }
        internal HotkeyDefinition FinishHotkey { get; private set; }

        internal SettingsForm(AppSettings settings)
        {
            Text = I18n.T("SettingsTitle");
            Width = 430;
            Height = 300;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            AddLabel(I18n.T("Language"), 20, 20, 130);
            languageCombo = new ComboBox
            {
                Left = 170,
                Top = 17,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            languageCombo.Items.Add(I18n.T("LanguageAuto"));
            languageCombo.Items.Add(I18n.T("LanguageZh"));
            languageCombo.Items.Add(I18n.T("LanguageEn"));
            languageCombo.SelectedIndex = settings.Language == "zh-CN" ? 1 : settings.Language == "en" ? 2 : 0;
            Controls.Add(languageCombo);

            AddLabel(I18n.T("CaptureHotkey"), 20, 65, 130);
            captureBox = CreateHotkeyBox(settings.Capture, 62);
            AddLabel(I18n.T("UndoHotkey"), 20, 105, 130);
            undoBox = CreateHotkeyBox(setti