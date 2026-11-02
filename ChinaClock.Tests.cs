using System;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;

internal static class ClockTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        string app = args.Length == 0 ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ChinaClock.exe") : args[0];
        if (!File.Exists(app))
        {
            Console.Error.WriteLine("FAIL: 北京时间程序尚未实现。");
            return 1;
        }
        MethodInfo format = Assembly.LoadFrom(app).GetType("ChinaClock.ChinaTime").GetMethod("Format");
        try
        {
            Check(format, new DateTimeOffset(2026, 10, 17, 16, 0, 0, TimeSpan.Zero),
                "北京时间 00:00:00\n2026-10-18 周日", "中国日期跨日");
            Check(format, new DateTimeOffset(2026, 12, 31, 16, 0, 0, TimeSpan.Zero),
                "北京时间 00:00:00\n2027-01-01 周五", "中国日期跨年");
            Check(format, new DateTimeOffset(2026, 10, 17, 8, 32, 1, TimeSpan.FromHours(-7)),
                "北京时间 23:32:01\n2026-10-17 周六", "美国夏令时不影响北京时间");
            Check(format, new DateTimeOffset(2026, 12, 17, 8, 32, 1, TimeSpan.FromHours(-8)),
                "北京时间 00:32:01\n2026-12-18 周五", "美国冬令时不影响北京时间");
            Type layout = Assembly.LoadFrom(app).GetType("ChinaClock.TaskbarLayout");
            if (layout == null) throw new Exception("尚未实现任务栏内部固定布局。");
            MethodInfo place = layout.GetMethod("Place");
            CheckPlacement(place, new Rectangle(0, 1032, 1920, 48), 1f);
            CheckPlacement(place, new Rectangle(-1920, 1032, 1920, 48), 1f);
            CheckPlacement(place, new Rectangle(0, 1368, 2560, 72), 1.5f);
            Type almanac = Assembly.LoadFrom(app).GetType("ChinaClock.ChineseAlmanac");
            if (almanac == null) throw new Exception("尚未实现农历、节气与黄历。");
            MethodInfo read = almanac.GetMethod("Read");
            CheckAlmanac(read, new DateTimeOffset(2026, 2, 16, 16, 0, 0, TimeSpan.Zero),
                "LunarDate", "正月初一", "中国午夜进入农历新年");
            CheckAlmanac(read, new DateTimeOffset(2025, 7, 25, 0, 0, 0, TimeSpan.Zero),
                "LunarDate", "闰六月初一", "正确处理闰月");
            CheckAlmanac(read, new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero),
                "CurrentTerm", "寒露", "非交节日仍显示当前节气");
            CheckAlmanac(read, new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero),
                "CurrentTerm", "霜降", "节气交接后更新");
            object newYear = read.Invoke(null, new object[] { new DateTimeOffset(2026, 2, 17, 4, 0, 0, TimeSpan.Zero) });
            string details = (string)newYear.GetType().GetProperty("Details").GetValue(newYear, null);
            if (!details.Contains("春节") || !details.Contains("宜：") || !details.Contains("忌："))
                throw new Exception("缺少节日或黄历宜忌详情。");
            CheckTextFits("北京时间 23:59:59  2026-10-18 周日", 10f, FontStyle.Bold);
            CheckTextFits("农历 闰十一月二十九 · 霜降 · 丙午马年", 9f, FontStyle.Regular);
            Console.WriteLine("PASS: 14 项日期、任务栏定位、农历黄历和文字宽度测试通过。");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.Message);
            return 1;
        }
    }

    private static void Check(MethodInfo method, DateTimeOffset instant, string expected, string name)
    {
        string actual = (string)method.Invoke(null, new object[] { instant });
        if (actual != expected)
            throw new Exception(name + ": " + actual.Replace("\n", " / "));
    }

    private static void CheckPlacement(MethodInfo method, Rectangle taskbar, float scale)
    {
        Rectangle clock = (Rectangle)method.Invoke(null, new object[] { taskbar, scale });
        if (!taskbar.Contains(clock)) throw new Exception("时钟超出任务栏，遮挡桌面。");
        if (clock.Left != taskbar.Left + (int)Math.Round(8 * scale))
            throw new Exception("时钟没有固定在任务栏最左侧。");
        if (clock.Height < taskbar.Height - (int)Math.Round(4 * scale))
            throw new Exception("没有利用任务栏高度显示日期和时间。");
    }

    private static void CheckAlmanac(MethodInfo read, DateTimeOffset instant, string property, string expected, string name)
    {
        object day = read.Invoke(null, new object[] { instant });
        string actual = (string)day.GetType().GetProperty(property).GetValue(day, null);
        if (actual != expected) throw new Exception(name + ": " + actual);
    }

    private static void CheckTextFits(string text, float size, FontStyle style)
    {
        using (Font font = new Font("Segoe UI", size, style))
        {
            Size measured = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            if (measured.Width > 272) throw new Exception("文字超出任务栏时钟宽度：" + measured.Width);
        }
    }
}
