using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ChinaClock
{
    public static class ChinaTime
    {
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

        public static string Format(DateTimeOffset instant)
        {
            DateTimeOffset china = InChina(instant);
            return "北京时间 " + china.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                + "\n" + china.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + " 周" + china.ToString("ddd", Chinese).Replace("周", "");
        }

        public static DateTimeOffset InChina(DateTimeOffset instant)
        {
            return TimeZoneInfo.ConvertTime(instant, Zone);
        }
    }

    public sealed class AlmanacDay
    {
        public DateTime Date { get; internal set; }
        public string LunarDate { get; internal set; }
        public string CurrentTerm { get; internal set; }
        public string Summary { get; internal set; }
        public string Tooltip { get; internal set; }
        public string Details { get; internal set; }
        public DateTimeOffset NextTermAt { get; internal set; }
    }

    public static class ChineseAlmanac
    {
        public static AlmanacDay Read(DateTimeOffset instant)
        {
            DateTimeOffset china = ChinaTime.InChina(instant);
            global::Lunar.Lunar lunar = global::Lunar.Lunar.FromDate(china.DateTime);
            global::Lunar.JieQi current = lunar.GetPrevJieQi();
            global::Lunar.JieQi next = lunar.GetNextJieQi();
            string lunarDate = lunar.MonthInChinese + "月" + lunar.DayInChinese;
            string year = lunar.YearInGanZhi + lunar.YearShengXiao + "年";
            string festivals = string.Join("、", lunar.Festivals.ToArray());
            string yi = string.Join("、", lunar.DayYi.ToArray());
            string ji = string.Join("、", lunar.DayJi.ToArray());
            DateTimeOffset nextAt = new DateTimeOffset(next.Solar.Year, next.Solar.Month, next.Solar.Day,
                next.Solar.Hour, next.Solar.Minute, next.Solar.Second, TimeSpan.FromHours(8));
            string summary = "农历 " + lunarDate + " · " + current.Name + " · " + year;
            string heading = china.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "  " + summary;
            string terms = "节气：" + current.Name + "  物候：" + lunar.WuHou
                + "\n下个节气：" + next.Name + "  " + nextAt.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + "（北京时间）";
            string stem = "干支：" + lunar.YearInGanZhi + "年 " + lunar.MonthInGanZhi + "月 " + lunar.DayInGanZhi + "日";
            string festivalLine = festivals.Length == 0 ? "" : "\n节日：" + festivals;
            string details = heading + festivalLine + "\n\n" + stem + "\n" + terms
                + "\n值神：" + lunar.DayTianShen + "（" + lunar.DayTianShenType + "，" + lunar.DayTianShenLuck + "）"
                + "\n冲煞：冲" + lunar.DayChongDesc + "，煞" + lunar.DaySha
                + "\n\n宜：" + Wrap(lunar.DayYi.ToArray()) + "\n忌：" + Wrap(lunar.DayJi.ToArray())
                + "\n\n黄历宜忌为民俗参考。数据：lunar-csharp 1.6.8（默认流派）。\n所有日期按北京时间计算，不改变系统时间。";
            return new AlmanacDay {
                Date = china.Date, LunarDate = lunarDate, CurrentTerm = current.Name,
                Summary = summary, Details = details, NextTermAt = nextAt,
                Tooltip = heading + festivalLine + "\n" + stem + "\n" + terms
                    + "\n宜：" + Preview(yi) + "\n忌：" + Preview(ji)
                    + "\n右键 → 今日黄历，查看完整内容（民俗参考）"
            };
        }

        private static string Preview(string value)
        {
            return value.Length > 36 ? value.Substring(0, 36) + "…" : value;
        }

        private static string Wrap(string[] items)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) text.Append(i % 8 == 0 ? "\n        " : "、");
                text.Append(items[i]);
            }
            return text.ToString();
        }
    }

    public static class TaskbarLayout
    {
        public static Rectangle Place(Rectangle taskbar, float scale)
        {
            int margin = (int)Math.Round(8 * scale);
            int inset = Math.Max(1, (int)Math.Round(2 * scale));
            return new Rectangle(taskbar.Left + margin, taskbar.Top + inset,
                Math.Min((int)Math.Round(280 * scale), taskbar.Width - margin * 2),
                taskbar.Height - inset * 2);
        }
    }

    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // 单文件模式：优先从本 exe 内嵌资源解析 lunar 程序集，
            // 使发布版无需在 exe 旁附带 lunar.dll（资源不存在时返回 null，回退到磁盘探测）。
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            bool firstInstance;
            using (Mutex instance = new Mutex(true, "Local\\ChinaClock.BeijingTime", out firstInstance))
            {
                if (!firstInstance) return;
                // 任何未处理异常都不允许拖垮消息循环/定时器：吞掉后继续在下一个 Tick 自愈。
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate { };
                AppDomain.CurrentDomain.UnhandledException += delegate { };
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ClockWindow());
            }
        }

        private static System.Reflection.Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
        {
            try
            {
                string simple = new System.Reflection.AssemblyName(args.Name).Name;
                if (!string.Equals(simple, "lunar", StringComparison.OrdinalIgnoreCase)) return null;
                using (System.IO.Stream stream = typeof(Program).Assembly
                    .GetManifestResourceStream("ChinaClock.embedded.lunar.dll"))
                {
                    if (stream == null) return null;
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    return System.Reflection.Assembly.Load(buffer);
                }
            }
            catch { return null; }
        }
    }

    internal sealed class ClockWindow : Form
    {
        private const string StartupPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupName = "ChinaClock";
        private readonly System.Windows.Forms.Timer timer;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem startupItem;
        private readonly Font timeFont;
        private readonly Font dateFont;
        private readonly ToolTip tip;
        private Color accent;
        private string display;
        private AlmanacDay almanac;
        private float scale;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        public ClockWindow()
        {
            Text = "北京时间 · China Clock";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            DoubleBuffered = true;
            UpdateTheme();
            using (Graphics graphics = CreateGraphics()) scale = graphics.DpiX / 96f;
            ClientSize = new Size(Scale(280), Scale(44));
            timeFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            dateFont = new Font("Segoe UI", 9f, FontStyle.Regular);

            menu = new ContextMenuStrip();
            ToolStripMenuItem showItem = new ToolStripMenuItem("显示北京时间", null, delegate { ShowClock(); });
            menu.Items.Add(showItem);
            menu.Items.Add("固定在任务栏最左侧", null, delegate { PlaceByTaskbar(); });
            menu.Items.Add("今日黄历（农历 / 节气 / 宜忌）", null, delegate
            {
                UpdateClock();
                MessageBox.Show(this, almanac.Details, "今日黄历 · 北京时间", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add("复制北京时间", null, delegate
            {
                try { Clipboard.SetText(ChinaTime.Format(DateTimeOffset.UtcNow).Replace("\n", " ")); }
                catch (ExternalException) { ShowNotice("剪贴板正被其他程序占用，请稍后重试。"); }
            });
            menu.Items.Add(new ToolStripSeparator());
            startupItem = new ToolStripMenuItem("开机自动启动");
            startupItem.Click += ToggleStartup;
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Close(); });
            menu.Opening += delegate { startupItem.Checked = IsStartupEnabled(); };
            ContextMenuStrip = menu;

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Information;
            tray.Text = "北京时间 · China Clock";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowClock(); };
            tray.Visible = true;
            tip = new ToolTip();
            tip.InitialDelay = 400;
            tip.ReshowDelay = 200;
            tip.AutoPopDelay = 30000;
            tip.ShowAlways = true;

            PlaceByTaskbar();
            UpdateClock();
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 250;
            timer.Tick += delegate { OnTick(); };
            timer.Start();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        // 每个 Tick 都独立兜底：任何一步抛异常都不影响其它步，更不能让定时器停摆。
        private void OnTick()
        {
            try { PlaceByTaskbar(); } catch { }
            try { KeepOnTop(); } catch { }
            try { UpdateClock(); } catch { }
        }

        // 自愈核心：无条件把窗口重新压回 topmost 层最顶端。
        // 任务栏本身是 topmost 窗口，时钟矩形又落在任务栏区域内，
        // 只有始终位于 topmost 层顶部才不会被任务栏盖住。
        private void KeepOnTop()
        {
            if (!IsHandleCreated || !Visible) return;
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // HWND_TOPMOST | NOSIZE|NOMOVE|NOACTIVATE
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { PlaceByTaskbar(); } catch { }
            try { KeepOnTop(); } catch { }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // 0x08000000 不抢焦点 | 0x00000080 不占任务栏按钮 | 0x00000008 创建即置顶层。
                parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;
                return parameters;
            }
        }

        private int Scale(int value) { return (int)Math.Round(value * scale); }

        private void UpdateClock()
        {
            DateTimeOffset instant = DateTimeOffset.UtcNow;
            if (almanac == null || ChinaTime.InChina(instant).Date != almanac.Date || instant >= almanac.NextTermAt)
            {
                almanac = ChineseAlmanac.Read(instant);
                tip.SetToolTip(this, almanac.Tooltip);
            }
            string time = ChinaTime.Format(instant);
            string next = time.Replace("\n", "  ") + "\n" + almanac.Summary;
            if (next != display)
            {
                display = next;
                tray.Text = time.Replace("\n", " ");
                UpdateTheme();
                Invalidate();
            }
        }

        private void UpdateTheme()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                bool light = key != null && (int)key.GetValue("SystemUsesLightTheme", 0) == 1;
                BackColor = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
                ForeColor = light ? Color.FromArgb(25, 25, 25) : Color.White;
                accent = light ? Color.FromArgb(40, 70, 100) : Color.FromArgb(180, 205, 230);
            }
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            string[] lines = (display ?? "\n").Split('\n');
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(args.Graphics, lines[0], timeFont,
                new Rectangle(Scale(4), 0, Width - Scale(8), Height / 2), ForeColor, flags);
            TextRenderer.DrawText(args.Graphics, lines[1], dateFont,
                new Rectangle(Scale(4), Height / 2, Width - Scale(8), Height - Height / 2), accent, flags);
        }

        private void PlaceByTaskbar()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            NativeRectangle rectangle;
            if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out rectangle)) return;
            Rectangle area = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            if (area.Height < Scale(24) || area.Width < Scale(240)) return;
            Rectangle target = TaskbarLayout.Place(area, scale);
            if (Bounds != target) Bounds = target;
            if (IsHandleCreated)
                SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        }

        private void OnDisplayChanged(object sender, EventArgs args)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                if (IsDisposed) return;
                try { PlaceByTaskbar(); } catch { }
                try { KeepOnTop(); } catch { }
            });
        }

        private void ShowClock()
        {
            if (!Visible) Show();
            TopMost = true;
            Invalidate();
        }

        private bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupPath))
                return key != null && string.Equals(key.GetValue(StartupName) as string,
                    "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase);
        }

        private void ToggleStartup(object sender, EventArgs args)
        {
            try
            {
                bool enabled = IsStartupEnabled();
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupPath))
                {
                    if (enabled) key.DeleteValue(StartupName, false);
                    else key.SetValue(StartupName, "\"" + Application.ExecutablePath + "\"");
                }
                startupItem.Checked = !enabled;
            }
            catch (UnauthorizedAccessException) { ShowNotice("当前用户没有修改开机启动项的权限。"); }
            catch (System.Security.SecurityException) { ShowNotice("系统策略不允许修改开机启动项。"); }
        }

        private void ShowNotice(string message)
        {
            tray.ShowBalloonTip(5000, "北京时间", message, ToolTipIcon.Info);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
                if (timer != null) timer.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                if (tip != null) tip.Dispose();
                if (menu != null) menu.Dispose();
                if (timeFont != null) timeFont.Dispose();
                if (dateFont != null) dateFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
