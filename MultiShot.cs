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
            undoBox = CreateHotkeyBox(settings.Undo, 102);
            AddLabel(I18n.T("FinishHotkey"), 20, 145, 130);
            finishBox = CreateHotkeyBox(settings.Finish, 142);

            helpLabel = new Label
            {
                Left = 20,
                Top = 182,
                Width = 370,
                Height = 34,
                Text = I18n.T("HotkeyHelp")
            };
            Controls.Add(helpLabel);

            resetButton = new Button
            {
                Left = 20,
                Top = 226,
                Width = 110,
                Height = 28,
                Text = I18n.T("ResetDefaults")
            };
            resetButton.Click += delegate
            {
                SetBoxHotkey(captureBox, HotkeyDefinition.CaptureDefault());
                SetBoxHotkey(undoBox, HotkeyDefinition.UndoDefault());
                SetBoxHotkey(finishBox, HotkeyDefinition.FinishDefault());
            };
            Controls.Add(resetButton);

            cancelButton = new Button
            {
                Left = 206,
                Top = 226,
                Width = 84,
                Height = 28,
                Text = I18n.T("Close"),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancelButton);

            saveButton = new Button
            {
                Left = 300,
                Top = 226,
                Width = 90,
                Height = 28,
                Text = I18n.T("Save")
            };
            saveButton.Click += OnSave;
            Controls.Add(saveButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void AddLabel(string text, int left, int top, int width)
        {
            Label label = new Label { Left = left, Top = top, Width = width, Height = 24, Text = text, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(label);
        }

        private TextBox CreateHotkeyBox(HotkeyDefinition hotkey, int top)
        {
            TextBox box = new TextBox
            {
                Left = 170,
                Top = top,
                Width = 220,
                ReadOnly = true,
                Tag = hotkey.Clone(),
                Text = hotkey.ToDisplayString()
            };
            box.KeyDown += OnHotkeyKeyDown;
            Controls.Add(box);
            return box;
        }

        private static void SetBoxHotkey(TextBox box, HotkeyDefinition hotkey)
        {
            box.Tag = hotkey.Clone();
            box.Text = hotkey.ToDisplayString();
        }

        private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            TextBox box = sender as TextBox;
            if (box == null) return;
            e.SuppressKeyPress = true;
            e.Handled = true;

            if (HotkeyDefinition.IsModifierKey(e.KeyCode)) return;
            if (e.KeyCode == Keys.Escape && !e.Control && !e.Alt && !e.Shift) return;

            uint modifiers = 0;
            if (e.Control) modifiers |= NativeMethods.MOD_CONTROL;
            if (e.Alt) modifiers |= NativeMethods.MOD_ALT;
            if (e.Shift) modifiers |= NativeMethods.MOD_SHIFT;

            if (modifiers == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            SetBoxHotkey(box, new HotkeyDefinition(modifiers, e.KeyCode));
        }

        private void OnSave(object sender, EventArgs e)
        {
            HotkeyDefinition capture = ((HotkeyDefinition)captureBox.Tag).Clone();
            HotkeyDefinition undo = ((HotkeyDefinition)undoBox.Tag).Clone();
            HotkeyDefinition finish = ((HotkeyDefinition)finishBox.Tag).Clone();

            if (capture.Modifiers == 0 || undo.Modifiers == 0 || finish.Modifiers == 0)
            {
                MessageBox.Show(I18n.T("ModifierRequired"), "MultiShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (capture.SameAs(undo) || capture.SameAs(finish) || undo.SameAs(finish))
            {
                MessageBox.Show(I18n.T("DuplicateHotkeys"), "MultiShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedLanguage = languageCombo.SelectedIndex == 1 ? "zh-CN" : languageCombo.SelectedIndex == 2 ? "en" : "auto";
            CaptureHotkey = capture;
            UndoHotkey = undo;
            FinishHotkey = finish;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                if (!NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    NativeMethods.SetProcessDPIAware();
            }
            catch
            {
                try { NativeMethods.SetProcessDPIAware(); } catch { }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppSettings settings = AppSettings.Load();
            I18n.SetLanguage(settings.Language);

            bool createdNew;
            using (Mutex singleInstance = new Mutex(true, @"Local\MultiShotSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(I18n.T("AlreadyRunning", settings.Capture.ToDisplayString()),
                        "MultiShot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                TempStorage.CleanupOldSessions();
                Application.Run(new MainForm(settings));
            }
        }
    }

    internal static class TempStorage
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "MultiShotClipboard");

        internal static string CreateSessionFolder()
        {
            Directory.CreateDirectory(Root);
            string folder = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        internal static void CleanupOldSessions()
        {
            try
            {
                if (!Directory.Exists(Root)) return;
                foreach (string dir in Directory.GetDirectories(Root))
                {
                    try
                    {
                        if (Directory.GetCreationTime(dir) < DateTime.Now.AddDays(-2))
                            Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    internal sealed class MainForm : Form
    {
        private const int HOTKEY_CAPTURE = 1001;
        private const int HOTKEY_FINISH = 1002;
        private const int HOTKEY_UNDO = 1003;

        private readonly Label statusLabel;
        private readonly Label hintLabel;
        private readonly Button captureButton;
        private readonly Button undoButton;
        private readonly Button finishButton;
        private readonly Button cancelButton;
        private readonly Button settingsButton;
        private readonly NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private readonly AppSettings settings;
        private readonly List<string> capturedFiles = new List<string>();
        private readonly List<string> completedFoldersAwaitingCleanup = new List<string>();
        private readonly System.Windows.Forms.Timer clipboardCleanupTimer;

        private string sessionFolder;
        private int nextShotIndex = 1;
        private bool isCapturing;
        private bool allowExit;
        private bool captureHotkeyOk;
        private bool finishHotkeyOk;
        private bool undoHotkeyOk;

        internal MainForm(AppSettings settings)
        {
            this.settings = settings;
            Text = "MultiShot";
             Width = 638;
            Height = 132;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            statusLabel = new Label
            {
                Left = 14,
                Top = 12,
                Width = 598,
                Height = 22,
                Text = I18n.T("NoShots")
            };

            hintLabel = new Label
            {
                Left = 14,
                Top = 34,
                Width = 598,
                Height = 20,
                Text = string.Empty
            };

            captureButton = new Button
            {
                Left = 14,
                Top = 64,
                Width = 96,
                Height = 28,
                Text = I18n.T("Capture")
            };
            captureButton.Click += delegate { CaptureOne(true); };

            undoButton = new Button
            {
                Left = 118,
                Top = 64,
                Width = 110,
                Height = 28,
                Text = I18n.T("Undo")
            };
            undoButton.Click += delegate { UndoLast(); };

            finishButton = new Button
            {
                Left = 236,
                Top = 64,
                Width = 120,
                Height = 28,
                Text = I18n.T("Finish")
            };
            finishButton.Click += delegate { FinishSession(); };

            cancelButton = new Button
            {
                Left = 364,
                Top = 64,
                Width = 112,
                Height = 28,
                Text = I18n.T("Cancel")
            };
            cancelButton.Click += delegate { CancelSession(); };

            settingsButton = new Button
            {
                Left = 484,
                Top = 64,
                Width = 128,
                Height = 28,
                Text = I18n.T("Settings")
            };
            settingsButton.Click += delegate { OpenSettings(); };

            Controls.Add(statusLabel);
            Controls.Add(hintLabel);
            Controls.Add(captureButton);
            Controls.Add(undoButton);
            Controls.Add(finishButton);
            Controls.Add(cancelButton);
            Controls.Add(settingsButton);

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            if (appIcon != null) Icon = appIcon;

            trayIcon = new NotifyIcon
            {
                Icon = appIcon ?? SystemIcons.Application,
                Text = I18n.T("TrayIdle"),
                Visible = true
            };
            RebuildTrayMenu();
            trayIcon.DoubleClick += delegate { CaptureOne(false); };

            clipboardCleanupTimer = new System.Windows.Forms.Timer();
            clipboardCleanupTimer.Interval = 15000;
            clipboardCleanupTimer.Tick += delegate { CleanupCompletedFoldersIfSafe(); };
            clipboardCleanupTimer.Start();

            Shown += delegate { PlaceBottomRight(); };

            FormClosing += OnFormClosing;
            UpdateUi();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterCurrentHotkeys();
            UpdateUi();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_CAPTURE); } catch { }
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_UNDO); } catch { }
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_FINISH); } catch { }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_CAPTURE)
                {
                     BeginInvoke((MethodInvoker)delegate { CaptureOne(false); });
                    return;
                }
                if (id == HOTKEY_UNDO)
                {
                    BeginInvoke((MethodInvoker)delegate { UndoLast(); });
                    return;
                }
                if (id == HOTKEY_FINISH)
                {
                    BeginInvoke((MethodInvoker)delegate { FinishSession(); });
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                trayIcon.ShowBalloonTip(1200, I18n.T("StillRunningTitle"),
                    I18n.T("StillRunningBody", settings.Capture.ToDisplayString()), ToolTipIcon.Info);
                return;
            }

            try
            {
                clipboardCleanupTimer.Stop();
                clipboardCleanupTimer.Dispose();
            }
            catch { }

            trayIcon.Visible = false;
            trayIcon.Dispose();
            if (trayMenu != null) trayMenu.Dispose();
        }

        private void PlaceBottomRight()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - 16, work.Bottom - Height - 16);
        }

        private void CaptureOne(bool restoreControllerAfter)
        {
            if (isCapturing) return;
            isCapturing = true;
            bool captured = false;

            try
            {
                CaptureToastForm.DismissActive();
                Hide();
                Application.DoEvents();
                Thread.Sleep(90);

                using (Bitmap shot = RegionCaptureForm.CaptureRegion(capturedFiles.Count))
                {
                    if (shot != null)
                    {
                        if (string.IsNullOrEmpty(sessionFolder))
                            sessionFolder = TempStorage.CreateSessionFolder();

                        string file;
                        do
                        {
                            file = Path.Combine(sessionFolder,
                                "shot-" + nextShotIndex.ToString("000") + ".png");
                            nextShotIndex++;
                        }
                        while (File.Exists(file));

                        shot.Save(file, ImageFormat.Png);
                        capturedFiles.Add(file);
                        captured = true;
                        UpdateTrayText();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(I18n.T("CaptureFailed", ex.Message), "MultiShot",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isCapturing = false;
                UpdateUi();

                if (restoreControllerAfter)
                {
                    Show();
                    PlaceBottomRight();
                }
                else
                {
                    Hide();
                }

                if (captured)
                    CaptureToastForm.ShowCaptureCount(capturedFiles.Count, settings.Finish.ToDisplayString());
            }
        }

        private void UndoLast()
        {
            ReconcileActiveCaptureFiles();
            if (capturedFiles.Count == 0)
            {
                statusLabel.Text = I18n.T("NothingToUndo");
                return;
            }

            string file = capturedFiles[capturedFiles.Count - 1];
            capturedFiles.RemoveAt(capturedFiles.Count - 1);
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }

            statusLabel.Text = capturedFiles.Count > 0
                ? I18n.T("UndoRemaining", capturedFiles.Count)
                : I18n.T("UndoEmpty");
            UpdateTrayText();
            UpdateUi();
        }

        private void FinishSession()
        {
            CaptureToastForm.DismissActive();
            ReconcileActiveCaptureFiles();
            if (capturedFiles.Count == 0)
            {
                statusLabel.Text = I18n.T("NeedShot");
                Show();
                return;
            }

            try
            {
                DataObject data = new DataObject();
                StringCollection files = new StringCollection();
                files.AddRange(capturedFiles.ToArray());
                data.SetFileDropList(files);

                using (Bitmap preview = ClipboardImageBuilder.CreatePreview(capturedFiles))
                {
                    if (preview != null)
                        data.SetData(DataFormats.Bitmap, true, new Bitmap(preview));
                }

                bool copied = false;
                Exception lastError = null;
                for (int i = 0; i < 6 && !copied; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(data, true);
                        copied = true;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        Thread.Sleep(100);
                    }
                }

                if (!copied)
                    throw lastError ?? new Exception(I18n.T("ClipboardUnavailable"));

                int count = capturedFiles.Count;
                string completedFolder = sessionFolder;
                capturedFiles.Clear();
                sessionFolder = null;
                nextShotIndex = 1;
                if (!string.IsNullOrEmpty(completedFolder))
                    completedFoldersAwaitingCleanup.Add(completedFolder);
                statusLabel.Text = I18n.T("CopiedStatus", count);
                UpdateTrayText();
                trayIcon.ShowBalloonTip(1200, I18n.T("CopiedTitle", count),
                    I18n.T("CopiedBody"), ToolTipIcon.Info);
                UpdateUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(I18n.T("ClipboardFailed", ex.Message), "MultiShot",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CancelSession()
        {
            CaptureToastForm.DismissActive();
            string folder = sessionFolder;
            capturedFiles.Clear();
            sessionFolder = null;
            nextShotIndex = 1;
            try
            {
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch { }
            statusLabel.Text = I18n.T("Cancelled");
            UpdateTrayText();
            UpdateUi();
        }

        private void UpdateTrayText()
        {
            try
            {
                string text = capturedFiles.Count > 0
                    ? I18n.T("TrayPending", capturedFiles.Count)
                    : I18n.T("TrayIdle");
                trayIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }
            catch { }
        }

        private void CleanupCompletedFoldersIfSafe()
        {
            if (completedFoldersAwaitingCleanup.Count == 0)
            {
                ReconcileActiveCaptureFiles();
                return;
            }

            bool cleanedAny = false;
            bool clipboardStillHasOurFiles = false;
            List<string> clipboardFiles = new List<string>();
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection current = Clipboard.GetFileDropList();
                    foreach (string file in current)
                         clipboardFiles.Add(file);
                }
            }
            catch
            {
                return;
            }

            for (int i = completedFoldersAwaitingCleanup.Count - 1; i >= 0; i--)
            {
                string folder = completedFoldersAwaitingCleanup[i];
                clipboardStillHasOurFiles = false;

                foreach (string file in clipboardFiles)
                {
                    if (IsFileInsideFolder(file, folder))
                    {
                        clipboardStillHasOurFiles = true;
                        break;
                    }
                }

                if (!clipboardStillHasOurFiles)
                {
                    try
                    {
                        if (Directory.Exists(folder))
                            Directory.Delete(folder, true);
                        completedFoldersAwaitingCleanup.RemoveAt(i);
                        cleanedAny = true;
                    }
                    catch { }
                }
            }

            bool activeChanged = ReconcileActiveCaptureFiles();
            if ((cleanedAny || activeChanged) && capturedFiles.Count == 0)
            {
                statusLabel.Text = I18n.T("NoShots");
                UpdateTrayText();
                undoButton.Enabled = false;
                finishButton.Enabled = false;
                cancelButton.Enabled = false;
            }
        }

        private bool ReconcileActiveCaptureFiles()
        {
            int before = capturedFiles.Count;
            capturedFiles.RemoveAll(delegate(string file)
            {
                try { return string.IsNullOrEmpty(file) || !File.Exists(file); }
                catch { return true; }
            });

            if (capturedFiles.Count == 0 && before > 0)
            {
                string folder = sessionFolder;
                sessionFolder = null;
                nextShotIndex = 1;
                try
                {
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) &&
                        Directory.GetFileSystemEntries(folder).Length == 0)
                        Directory.Delete(folder, false);
                }
                catch { }
            }

            return capturedFiles.Count != before;
        }

        private static bool IsFileInsideFolder(string file, string folder)
        {
            try
            {
                string fullFile = Path.GetFullPath(file);
                string fullFolder = Path.GetFullPath(folder);
                if (!fullFolder.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    fullFolder += Path.DirectorySeparatorChar;
                return fullFile.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void RebuildTrayMenu()
        {
            ContextMenuStrip old = trayMenu;
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(I18n.T("ShowController"), null, delegate
            {
                Show();
                PlaceBottomRight();
            });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(I18n.T("Capture"), null, delegate { CaptureOne(false); });
            trayMenu.Items.Add(I18n.T("Undo"), null, delegate { UndoLast(); });
            trayMenu.Items.Add(I18n.T("Finish"), null, delegate { FinishSession(); });
            trayMenu.Items.Add(I18n.T("Cancel"), null, delegate { CancelSession(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(I18n.T("Settings"), null, delegate { OpenSettings(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(I18n.T("Exit"), null, delegate
            {
                allowExit = true;
                Close();
            });
             trayIcon.ContextMenuStrip = trayMenu;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
            }
        }

        private void RegisterCurrentHotkeys()
        {
            captureHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_CAPTURE,
                settings.Capture.Modifiers, (uint)settings.Capture.Key);
            undoHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_UNDO,
                settings.Undo.Modifiers, (uint)settings.Undo.Key);
            finishHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_FINISH,
                settings.Finish.Modifiers, (uint)settings.Finish.Key);
        }

        private void UnregisterAllHotkeys()
        {
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_CAPTURE); } catch { }
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_UNDO); } catch { }
            try { NativeMethods.UnregisterHotKey(Handle, HOTKEY_FINISH); } catch { }
            captureHotkeyOk = finishHotkeyOk = undoHotkeyOk = false;
        }

        private bool TryApplyHotkeys(HotkeyDefinition capture, HotkeyDefinition undo, HotkeyDefinition finish, out string failedHotkey)
        {
            failedHotkey = null;
            HotkeyDefinition oldCapture = settings.Capture.Clone();
            HotkeyDefinition oldUndo = settings.Undo.Clone();
            HotkeyDefinition oldFinish = settings.Finish.Clone();

            UnregisterAllHotkeys();
            bool capOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_CAPTURE, capture.Modifiers, (uint)capture.Key);
            if (!capOk) failedHotkey = capture.ToDisplayString();
            bool undoOk = false;
            bool finishOk = false;
            if (capOk)
            {
                undoOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_UNDO, undo.Modifiers, (uint)undo.Key);
                if (!undoOk) failedHotkey = undo.ToDisplayString();
            }
            if (capOk && undoOk)
            {
                finishOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_FINISH, finish.Modifiers, (uint)finish.Key);
                if (!finishOk) failedHotkey = finish.ToDisplayString();
            }

            if (!(capOk && undoOk && finishOk))
            {
                UnregisterAllHotkeys();
                captureHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_CAPTURE, oldCapture.Modifiers, (uint)oldCapture.Key);
                undoHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_UNDO, oldUndo.Modifiers, (uint)oldUndo.Key);
                finishHotkeyOk = NativeMethods.RegisterHotKey(Handle, HOTKEY_FINISH, oldFinish.Modifiers, (uint)oldFinish.Key);
                return false;
            }

            captureHotkeyOk = undoHotkeyOk = finishHotkeyOk = true;
            settings.Capture = capture.Clone();
            settings.Undo = undo.Clone();
            settings.Finish = finish.Clone();
            return true;
        }

        private void OpenSettings()
        {
            CaptureToastForm.DismissActive();
            using (SettingsForm dialog = new SettingsForm(settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                bool hotkeysChanged = !settings.Capture.SameAs(dialog.CaptureHotkey) ||
                    !settings.Undo.SameAs(dialog.UndoHotkey) ||
                    !settings.Finish.SameAs(dialog.FinishHotkey);
                if (hotkeysChanged)
                {
                    string failed;
                    if (!TryApplyHotkeys(dialog.CaptureHotkey, dialog.UndoHotkey, dialog.FinishHotkey, out failed))
                    {
                        MessageBox.Show(I18n.T("HotkeyConflict", failed), "MultiShot",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        UpdateUi();
                        return;
                    }
                }

                settings.Language = dialog.SelectedLanguage;
                 I18n.SetLanguage(settings.Language);
                try { settings.Save(); } catch { }
                ApplyLocalization();
                MessageBox.Show(I18n.T("SettingsSavedBody"), I18n.T("SettingsSaved"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ApplyLocalization()
        {
            captureButton.Text = I18n.T("Capture");
            undoButton.Text = I18n.T("Undo");
            finishButton.Text = I18n.T("Finish");
            cancelButton.Text = I18n.T("Cancel");
            settingsButton.Text = I18n.T("Settings");
            if (capturedFiles.Count == 0) statusLabel.Text = I18n.T("NoShots");
            RebuildTrayMenu();
            UpdateUi();
        }

        private void UpdateUi()
        {
            bool activeChanged = ReconcileActiveCaptureFiles();
            UpdateTrayText();

            if (capturedFiles.Count > 0)
            {
                statusLabel.Text = I18n.T("Capturing", capturedFiles.Count);
                undoButton.Enabled = true;
                finishButton.Enabled = true;
                cancelButton.Enabled = true;
            }
            else
            {
                if (activeChanged)
                    statusLabel.Text = I18n.T("NoShots");
                undoButton.Enabled = false;
                finishButton.Enabled = false;
                cancelButton.Enabled = false;
            }

            if (captureHotkeyOk && finishHotkeyOk && undoHotkeyOk)
                hintLabel.Text = I18n.T("Hint", settings.Capture.ToDisplayString(), settings.Undo.ToDisplayString(), settings.Finish.ToDisplayString());
            else
                hintLabel.Text = I18n.T("HintPartial");
        }
    }

    internal sealed class CaptureToastForm : Form
    {
        private static CaptureToastForm activeToast;
        private readonly System.Windows.Forms.Timer closeTimer;

        private CaptureToastForm(string text)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(32, 32, 32);
            Width = 250;
            Height = 52;
            Opacity = 0.93;

            Label label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Text = text
            };
            Controls.Add(label);

            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(work.Right - Width - 18, work.Bottom - Height - 18);

            closeTimer = new System.Windows.Forms.Timer();
            closeTimer.Interval = 1100;
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                Close();
            };
            Shown += delegate { closeTimer.Start(); };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        internal static void ShowCaptureCount(int count, string finishShortcut)
        {
            DismissActive();
            CaptureToastForm toast = new CaptureToastForm(
                I18n.T("Toast", count, finishShortcut));
            activeToast = toast;
            toast.FormClosed += delegate
            {
                if (object.ReferenceEquals(activeToast, toast))
                    activeToast = null;
                try { toast.Dispose(); } catch { }
            };
            toast.Show();
        }

        internal static void DismissActive()
        {
            CaptureToastForm toast = activeToast;
            activeToast = null;
            if (toast == null) return;
            try
            {
                if (!toast.IsDisposed) toast.Close();
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && closeTimer != null)
                closeTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class RegionCaptureForm : Form
    {
        private const int DragThreshold = 6;

        private readonly Bitmap desktop;
        private readonly Rectangle virtualScreen;
        private readonly uint currentProcessId;
        private Point start;
        private Point current;
        private bool mouseDown;
        private bool manualDrag;
        private Rectangle hoverWindowRect;
        private bool hasHoverWindow;
        private Bitmap resultBitmap;
        private readonly int currentCount;

        private RegionCaptureForm(int currentCount)
        {
            this.currentCount = currentCount;
            virtualScreen = SystemInformation.VirtualScreen;
            Bounds = virtualScreen;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
            currentProcessId = (uint)Process.GetCurrentProcess().Id;

            desktop = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(desktop))
            {
                g.CopyFromScreen(virtualScreen.Left, virtualScreen.Top, 0, 0,
                    virtualScreen.Size, CopyPixelOperation.SourceCopy);
            }

            MouseDown += OnMouseDownCapture;
            MouseMove += OnMouseMoveCapture;
            MouseUp += OnMouseUpCapture;
            KeyDown += OnKeyDownCapture;
            Shown += delegate
            {
                UpdateHoverWindow(PointToClient(Cursor.Position));
                Invalidate();
            };
        }

        internal static Bitmap CaptureRegion(int currentCount)
        {
            using (RegionCaptureForm form = new RegionCaptureForm(currentCount))
            {
                DialogResult result = form.ShowDialog();
                if (result == DialogResult.OK && form.resultBitmap != null)
                    return form.resultBitmap;
                return null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.DrawImageUnscaled(desktop, 0, 0);
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(118, 0, 0, 0)))
                e.Graphics.FillRectangle(shade, ClientRectangle);

            Rectangle selected = Rectangle.Empty;
            bool isManual = mouseDown && manualDrag;

            if (isManual)
                selected = Normalize(start, current);
            else if (hasHoverWindow)
                selected = hoverWindowRect;

            if (selected.Width > 0 && selected.Height > 0)
            {
                Rectangle clipped = Rectangle.Intersect(ClientRectangle, selected);
                if (clipped.Width > 0 && clipped.Height > 0)
                {
                    e.Graphics.DrawImage(desktop, clipped, clipped, GraphicsUnit.Pixel);
                    using (Pen pen = new Pen(isManual ? Color.White : Color.DeepSkyBlue, 2f))
                    {
                        pen.Alignment = PenAlignment.Inset;
                        e.Graphics.DrawRectangle(pen, clipped);
                    }

                    DrawSizeBadge(e.Graphics, clipped, isManual ? I18n.T("ModeManual") : I18n.T("ModeWindow"));
                }
            }

            if (isManual)
                DrawMagnifier(e.Graphics);

            DrawInstruction(e.Graphics);
            DrawCounterBadge(e.Graphics);
        }

        private void DrawInstruction(Graphics g)
        {
            string text = I18n.T("OverlayInstruction");
            using (Font font = new Font("Segoe UI", 10f, FontStyle.Regular))
            {
                SizeF size = g.MeasureString(text, font);
                RectangleF box = new RectangleF(18, 18, size.Width + 24, size.Height + 12);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
                    g.FillRectangle(bg, box);
                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(text, font, fg, box.Left + 12, box.Top + 6);
            }
        }


        private void DrawCounterBadge(Graphics g)
        {
            string text = I18n.T("OverlayCount", currentCount);
            using (Font font = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                SizeF size = g.MeasureString(text, font);
                float width = size.Width + 24;
                float height = size.Height + 12;
                float x = ClientRectangle.Right - width - 18;
                float y = 18;
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
                    g.FillRectangle(bg, x, y, width, height);
                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(text, font, fg, x + 12, y + 6);
            }
        }

        private void DrawMagnifier(Graphics g)
        {
            const int sourceSize = 11;
            const int scale = 8;
            int half = sourceSize / 2;
            int sourceX = Math.Max(0, Math.Min(desktop.Width - sourceSize, current.X - half));
            int sourceY = Math.Max(0, Math.Min(desktop.Height - sourceSize, current.Y - half));
            Rectangle src = new Rectangle(sourceX, sourceY, sourceSize, sourceSize);
            int lensSize = sourceSize * scale;

            int x = current.X + 24;
            int y = current.Y + 24;
            if (x + lensSize + 4 > ClientRectangle.Right) x = current.X - lensSize - 24;
            if (y + lensSize + 26 > ClientRectangle.Bottom) y = current.Y - lensSize - 34;
            x = Math.Max(4, Math.Min(ClientRectangle.Right - lensSize - 4, x));
            y = Math.Max(4, Math.Min(ClientRectangle.Bottom - lensSize - 26, y));

            Rectangle dst = new Rectangle(x, y, lensSize, lensSize);
            InterpolationMode oldMode = g.InterpolationMode;
            PixelOffsetMode oldPixel = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(desktop, dst, src, GraphicsUnit.Pixel);
            g.InterpolationMode = oldMode;
            g.PixelOffsetMode = oldPixel;

            using (Pen border = new Pen(Color.White, 2f))
                g.DrawRectangle(border, dst);

            int cx = dst.Left + lensSize / 2;
            int cy = dst.Top + lensSize / 2;
            using (Pen cross = new Pen(Color.FromArgb(230, 255, 70, 70), 1f))
            {
                g.DrawLine(cross, cx, dst.Top, cx, dst.Bottom);
                g.DrawLine(cross, dst.Left, cy, dst.Right, cy);
            }

            string pos = (current.X + virtualScreen.Left) + ", " + (current.Y + virtualScreen.Top);
            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            {
                SizeF s = g.MeasureString(pos, font);
                RectangleF label = new RectangleF(dst.Left, dst.Bottom + 2, Math.Max(dst.Width, s.Width + 12), s.Height + 5);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
                    g.FillRectangle(bg, label);
                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(pos, font, fg, label.Left + 6, label.Top + 2);
            }
        }

        private void DrawSizeBadge(Graphics g, Rectangle rect, string mode)
        {
            string sizeText = mode + "  " + rect.Width + " × " + rect.Height;
            using (Font font = new Font("Segoe UI", 10f, FontStyle.Regular))
            {
                SizeF s = g.MeasureString(sizeText, font);
                float x = rect.Left;
                float y = rect.Top - s.Height - 10;
                if (y < 4) y = Math.Min(ClientRectangle.Bottom - s.Height - 8, rect.Top + 6);
                if (x + s.Width + 12 > ClientRectangle.Right)
                    x = ClientRectangle.Right - s.Width - 12;
                if (x < 0) x = 0;

                using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
                    g.FillRectangle(bg, x, y, s.Width + 12, s.Height + 4);
                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(sizeText, font, fg, x + 6, y + 2);
            }
        }

        private void OnMouseDownCapture(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            start = e.Location;
            current = e.Location;
            mouseDown = true;
            manualDrag = false;
            Invalidate();
        }

        private void OnMouseMoveCapture(object sender, MouseEventArgs e)
        {
            current = e.Location;

            if (mouseDown)
            {
                if (!manualDrag && Distance(start, current) >= DragThreshold)
                    manualDrag = true;
            }
            else
            {
                UpdateHoverWindow(e.Location);
            }

            Invalidate();
        }

        private void OnMouseUpCapture(object sender, MouseEventArgs e)
        {
            if (!mouseDown || e.Button != MouseButtons.Left) return;
            current = e.Location;

            Rectangle rect;
            if (manualDrag)
            {
                rect = Normalize(start, current);
                if (rect.Width < 3 || rect.Height < 3)
                {
                    mouseDown = false;
                    manualDrag = false;
                    UpdateHoverWindow(e.Location);
                    Invalidate();
                    return;
                }
            }
            else
            {
                UpdateHoverWindow(e.Location);
                if (!hasHoverWindow)
                {
                    mouseDown = false;
                    Invalidate();
                    return;
                }
                rect = hoverWindowRect;
            }

            mouseDown = false;
            manualDrag = false;
            CaptureRectangle(rect);
        }

        private void CaptureRectangle(Rectangle rect)
        {
            Rectangle clipped = Rectangle.Intersect(ClientRectangle, rect);
            if (clipped.Width < 1 || clipped.Height < 1) return;

            resultBitmap = new Bitmap(clipped.Width, clipped.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(resultBitmap))
                g.DrawImage(desktop,
                    new Rectangle(0, 0, clipped.Width, clipped.Height),
                    clipped, GraphicsUnit.Pixel);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateHoverWindow(Point clientPoint)
        {
            Point screenPoint = new Point(clientPoint.X + virtualScreen.Left, clientPoint.Y + virtualScreen.Top);
            Rectangle screenRect;
            if (TryFindTopLevelWindowAt(screenPoint, out screenRect))
            {
                Rectangle clientRect = new Rectangle(
                    screenRect.Left - virtualScreen.Left,
                    screenRect.Top - virtualScreen.Top,
                    screenRect.Width,
                    screenRect.Height);

                hoverWindowRect = Rectangle.Intersect(ClientRectangle, clientRect);
                hasHoverWindow = hoverWindowRect.Width > 2 && hoverWindowRect.Height > 2;
            }
            else
            {
                hoverWindowRect = Rectangle.Empty;
                hasHoverWindow = false;
            }
        }

        private bool TryFindTopLevelWindowAt(Point screenPoint, out Rectangle rect)
        {
            rect = Rectangle.Empty;
            IntPtr hwnd = NativeMethods.GetTopWindow(IntPtr.Zero);

            while (hwnd != IntPtr.Zero)
            {
                try
                {
                    if (NativeMethods.IsWindowVisible(hwnd))
                    {
                        uint pid;
                        NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                        if (pid != 0 && pid != currentProcessId && !IsCloaked(hwnd))
                        {
                            Rectangle candidate;
                            if (TryGetWindowBounds(hwnd, out candidate) &&
                                candidate.Width > 20 && candidate.Height > 20 &&
                                candidate.Contains(screenPoint))
                            {
                                rect = candidate;
                                return true;
                            }
                        }
                    }
                }
                catch { }

                hwnd = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDNEXT);
            }

            return false;
        }

        private static bool IsCloaked(IntPtr hwnd)
        {
            try
            {
                int cloaked;
                int hr = NativeMethods.DwmGetWindowAttribute(hwnd,
                    NativeMethods.DWMWA_CLOAKED, out cloaked, sizeof(int));
                return hr == 0 && cloaked != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetWindowBounds(IntPtr hwnd, out Rectangle rect)
        {
            rect = Rectangle.Empty;
            NativeMethods.RECT nativeRect;

            try
            {
                int hr = NativeMethods.DwmGetWindowAttribute(hwnd,
                    NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out nativeRect, Marshal.SizeOf(typeof(NativeMethods.RECT)));
                if (hr != 0)
                {
                    if (!NativeMethods.GetWindowRect(hwnd, out nativeRect))
                        return false;
                }
            }
            catch
            {
                if (!NativeMethods.GetWindowRect(hwnd, out nativeRect))
                    return false;
            }

            int width = nativeRect.Right - nativeRect.Left;
            int height = nativeRect.Bottom - nativeRect.Top;
            if (width <= 0 || height <= 0) return false;

            rect = new Rectangle(nativeRect.Left, nativeRect.Top, width, height);
            return true;
        }

        private void OnKeyDownCapture(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private static double Distance(Point a, Point b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return Math.Sqrt((double)dx * dx + (double)dy * dy);
        }

        private static Rectangle Normalize(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int w = Math.Abs(a.X - b.X);
            int h = Math.Abs(a.Y - b.Y);
            return new Rectangle(x, y, w, h);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && desktop != null)
                desktop.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class ClipboardImageBuilder
    {
        internal static Bitmap CreatePreview(List<string> files)
        {
            if (files == null || files.Count == 0) return null;

            List<Image> images = new List<Image>();
            try
            {
                int maxOriginalWidth = 1;
                long totalOriginalHeight = 0;

                foreach (string file in files)
                {
                    Image img = Image.FromFile(file);
                    images.Add(img);
                    maxOriginalWidth = Math.Max(maxOriginalWidth, img.Width);
                    totalOriginalHeight += img.Height;
                }

                const int maxWidth = 1600;
                const int maxHeight = 12000;
                const int gap = 10;

                double scale = Math.Min(1.0, (double)maxWidth / maxOriginalWidth);
                double scaledHeight = totalOriginalHeight * scale + gap * Math.Max(0, images.Count - 1);
                if (scaledHeight > maxHeight)
                    scale *= (double)maxHeight / scaledHeight;

                int canvasWidth = 1;
                int canvasHeight = gap * Math.Max(0, images.Count - 1);
                foreach (Image img in images)
                {
                    canvasWidth = Math.Max(canvasWidth, Math.Max(1, (int)Math.Round(img.Width * scale)));
                    canvasHeight += Math.Max(1, (int)Math.Round(img.Height * scale));
                }

                Bitmap result = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    int y = 0;
                    foreach (Image img in images)
                    {
                        int w = Math.Max(1, (int)Math.Round(img.Width * scale));
                        int h = Math.Max(1, (int)Math.Round(img.Height * scale));
                        g.DrawImage(img, new Rectangle(0, y, w, h));
                        y += h + gap;
                    }
                }
                return result;
            }
            finally
            {
                foreach (Image img in images)
                    img.Dispose();
            }
        }
    }
}
