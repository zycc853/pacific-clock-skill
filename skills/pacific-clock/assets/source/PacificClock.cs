using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

[Serializable]
public class ClockSettings
{
    public int X = int.MinValue;
    public int Y = int.MinValue;
    public bool Compact = true;
    public bool Topmost = true;
    public bool ClickThrough = false;
    public bool TwelveHour = false;
    public bool Seconds = false;
    public bool OrdinaryWindow = false;
    public double Opacity = 1;
    public double Zoom = 1;
    public string Theme = "Paper";
}

internal static class Program
{
    internal static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    internal static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string SettingsPath = Path.Combine(BaseDir, "PacificClock.settings.xml");
    internal static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    internal static EventWaitHandle Wake;
    internal static EventWaitHandle Reveal;

    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--self-test")
                return SelfTest(args[1]);
            if (args.Length == 2 && args[0] == "--preview")
            {
                using (ClockForm form = new ClockForm(new ClockSettings { Compact = false }, true))
                    form.RenderPreview(args[1]);
                return 0;
            }
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\PacificClock.Desktop.v1", out first))
            using (Wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PacificClock.Wake.v1"))
            using (Reveal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PacificClock.Reveal.v1"))
            {
                bool locate = args.Length > 0 && args[0] == "--find";
                if (!first) { if (locate) Reveal.Set(); else Wake.Set(); return 0; }
                ClockSettings state = LoadSettings();
                if (locate) state.OrdinaryWindow = true;
                ClockForm form = new ClockForm(state, false);
                if (locate) form.Shown += delegate { form.ShowLocator(); };
                Application.Run(form);
            }
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("太平洋时钟无法启动：\n" + ex.Message, "太平洋时钟", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    internal static ClockSettings LoadSettings()
    {
        try
        {
            using (FileStream file = File.OpenRead(SettingsPath))
            {
                ClockSettings value = (ClockSettings)new XmlSerializer(typeof(ClockSettings)).Deserialize(file);
                if (double.IsNaN(value.Opacity) || value.Opacity < 0.45 || value.Opacity > 1) value.Opacity = 0.94;
                return value;
            }
        }
        catch (Exception ex)
        {
            if (!(ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)) throw;
            return new ClockSettings();
        }
    }

    internal static void SaveSettings(ClockSettings settings)
    {
        string temp = SettingsPath + ".tmp";
        try
        {
            using (FileStream file = File.Create(temp))
                new XmlSerializer(typeof(ClockSettings)).Serialize(file, settings);
            if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null);
            else File.Move(temp, SettingsPath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static string ZoneLabel(DateTime utc)
    {
        // Keep the UTC instant: a local 01:xx occurs twice at the autumn DST boundary.
        return Pacific.IsDaylightSavingTime(new DateTimeOffset(utc)) ? "PDT" : "PST";
    }

    internal static Point Clamp(Point position, Size size, Rectangle area)
    {
        return new Point(Math.Max(area.Left, Math.Min(position.X, area.Right - size.Width)),
            Math.Max(area.Top, Math.Min(position.Y, area.Bottom - size.Height)));
    }

    private static int SelfTest(string report)
    {
        StringBuilder result = new StringBuilder();
        int failed = 0;
        string[,] cases = {
            { "2026-01-15T12:00:00Z", "2026-01-15 04:00:00", "PST" },
            { "2026-07-15T12:00:00Z", "2026-07-15 05:00:00", "PDT" },
            { "2026-03-08T09:59:59Z", "2026-03-08 01:59:59", "PST" },
            { "2026-03-08T10:00:00Z", "2026-03-08 03:00:00", "PDT" },
            { "2026-11-01T08:59:59Z", "2026-11-01 01:59:59", "PDT" },
            { "2026-11-01T09:00:00Z", "2026-11-01 01:00:00", "PST" },
            { "2026-10-29T06:59:59Z", "2026-10-28 23:59:59", "PDT" },
            { "2026-10-29T07:00:00Z", "2026-10-29 00:00:00", "PDT" }
        };
        for (int i = 0; i < cases.GetLength(0); i++)
        {
            DateTime utc = DateTime.Parse(cases[i, 0], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            string actual = TimeZoneInfo.ConvertTimeFromUtc(utc, Pacific).ToString("yyyy-MM-dd HH:mm:ss", English);
            bool pass = actual == cases[i, 1] && ZoneLabel(utc) == cases[i, 2];
            if (!pass) failed++;
            result.AppendLine((pass ? "PASS " : "FAIL ") + cases[i, 0] + " => " + actual + " " + ZoneLabel(utc));
        }
        Point clamped = Clamp(new Point(99999, -99999), new Size(210, 88), new Rectangle(-1920, 0, 1920, 1040));
        bool placement = clamped == new Point(-210, 0);
        if (!placement) failed++;
        result.AppendLine((placement ? "PASS" : "FAIL") + " off-screen position recovery / negative monitor coordinates");
        ClockSettings original = new ClockSettings { X = -300, Y = 200, Compact = true, ClickThrough = true, Opacity = 0.7, Zoom = 0.65 };
        using (MemoryStream memory = new MemoryStream())
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ClockSettings));
            serializer.Serialize(memory, original);
            memory.Position = 0;
            ClockSettings restored = (ClockSettings)serializer.Deserialize(memory);
            bool roundtrip = restored.X == -300 && restored.Y == 200 && restored.Compact && restored.ClickThrough && restored.Opacity == 0.7 && restored.Zoom == 0.65;
            if (!roundtrip) failed++;
            result.AppendLine((roundtrip ? "PASS" : "FAIL") + " portable settings round-trip");
        }
        failed += ClockForm.VerifyWindowBehavior(result);
        result.AppendLine("Failed: " + failed);
        File.WriteAllText(report, result.ToString(), Encoding.UTF8);
        return failed == 0 ? 0 : 1;
    }
}

internal sealed class ClockForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly ClockSettings settings;
    private readonly bool preview;
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private NotifyIcon tray;
    private Icon clockIcon;
    private float scale = 1;
    private float dpiScale = 1;
    private bool dragging;
    private int resizeEdges;
    private Rectangle resizeStart;
    private double zoomStart;
    private Point dragStart, windowStart;
    private string lastStamp = "";
    private DateTime currentUtc = DateTime.UtcNow;
    private bool saveWarningShown;
    private bool locator;
    private readonly Button optionsButton = new Button();
    private readonly ToolTip tips = new ToolTip();

    private bool Dark { get { return settings.Theme == "Graphite"; } }
    private Color Surface { get { return Dark ? Color.FromArgb(34, 35, 39) : Color.FromArgb(248, 246, 240); } }
    private Color Ink { get { return Dark ? Color.FromArgb(246, 243, 238) : Color.FromArgb(40, 39, 34); } }
    private Color Muted { get { return Dark ? Color.FromArgb(179, 176, 170) : Color.FromArgb(113, 109, 99); } }
    private Color Edge { get { return Dark ? Color.FromArgb(65, 66, 71) : Color.FromArgb(222, 217, 206); } }
    private Color Accent { get { return Dark ? Color.FromArgb(213, 176, 134) : Color.FromArgb(128, 91, 62); } }
    private Color Badge { get { return Dark ? Color.FromArgb(49, 50, 55) : Color.FromArgb(235, 230, 219); } }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);

    internal ClockForm(ClockSettings value, bool isPreview)
    {
        settings = value;
        preview = isPreview;
        using (Graphics g = CreateGraphics()) dpiScale = g.DpiX / 96f;
        settings.Zoom = NormalizeZoom(settings.Zoom);
        scale = dpiScale * (float)settings.Zoom;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "太平洋时钟 · Pacific Clock";
        BackColor = Surface;
        DoubleBuffered = true;
        TopMost = settings.Topmost;
        ApplyOpacity();
        AccessibleName = "太平洋当前时间";
        AccessibleRole = AccessibleRole.Clock;
        Cursor = Cursors.SizeAll;
        optionsButton.FlatStyle = FlatStyle.Flat;
        optionsButton.FlatAppearance.BorderSize = 0;
        optionsButton.Text = "";
        // Paint the dots directly. WinForms caches Font handles in button layout;
        // replacing and disposing those fonts during resizing can break painting.
        optionsButton.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float diameter = Math.Max(1.5f, 2 * scale);
            using (SolidBrush brush = new SolidBrush(Muted))
                for (int i = -1; i <= 1; i++)
                    e.Graphics.FillEllipse(brush, optionsButton.Width / 2f + i * 6 * scale - diameter / 2,
                        optionsButton.Height / 2f - diameter / 2, diameter, diameter);
        };
        optionsButton.TextAlign = ContentAlignment.MiddleCenter;
        optionsButton.Cursor = Cursors.Hand;
        optionsButton.TabStop = true;
        optionsButton.AccessibleName = "时钟设置";
        optionsButton.AccessibleDescription = "打开外观、显示和运行选项";
        optionsButton.Click += delegate { menu.Show(optionsButton, new Point(optionsButton.Width, optionsButton.Height), ToolStripDropDownDirection.BelowLeft); };
        tips.SetToolTip(optionsButton, "设置 · 右键时钟也可打开");
        Controls.Add(optionsButton);
        ApplyTheme();
        ApplySize();
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        if (settings.X == int.MinValue || settings.Y == int.MinValue)
            Location = new Point(area.Right - Width - S(20), area.Bottom - Height - S(20));
        else
        {
            Point saved = new Point(settings.X, settings.Y);
            Location = Program.Clamp(saved, Size, Screen.FromPoint(saved).WorkingArea);
        }
        BuildMenu();
        ContextMenuStrip = menu;
        if (!preview)
        {
            if (settings.OrdinaryWindow) Shown += delegate { ShowOrdinary(false); };
            clockIcon = MakeIcon();
            Icon = clockIcon;
            tray = new NotifyIcon { Icon = clockIcon, Visible = true, Text = "太平洋时钟", ContextMenuStrip = menu };
            tray.DoubleClick += delegate { RestoreClock(); };
            timer.Interval = 1000;
            timer.Tick += delegate { TickClock(); };
            timer.Start();
            TickClock();
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
        }
    }

    protected override bool ShowWithoutActivation { get { return !locator; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            if (!locator) cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            else { cp.ExStyle &= ~(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE); cp.ExStyle |= 0x40000; }
            if (settings != null && settings.ClickThrough) cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    private int S(float value) { return (int)Math.Round(value * scale); }
    private Font UiFont(float size, FontStyle style) { return new Font("Segoe UI", size * scale, style, GraphicsUnit.Pixel); }

    private void ApplyOpacity()
    {
        // Windows passes clicks through layered windows. Keep layering enabled
        // even when the user selects 100% while click-through is on.
        Opacity = settings.ClickThrough && settings.Opacity == 1 ? 0.999 : settings.Opacity;
    }

    private void ApplySize()
    {
        scale = dpiScale * (float)NormalizeZoom(settings.Zoom);
        ClientSize = settings.Compact ? new Size(S(settings.Seconds ? 202 : 170), S(54)) : new Size(S(248), S(160));
        optionsButton.Visible = !settings.Compact;
        optionsButton.SetBounds(Width - S(49), S(11), S(36), S(32));
        using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width, Height), S(settings.Compact ? 19 : 22)))
        {
            Region previous = Region;
            Region = new Region(path);
            if (previous != null) previous.Dispose();
        }
    }

    private static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (GraphicsPath border = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), S(settings.Compact ? 19 : 22)))
        using (Pen pen = new Pen(Edge, scale)) g.DrawPath(pen, border);
        DateTime pacific = TimeZoneInfo.ConvertTimeFromUtc(currentUtc, Program.Pacific);
        string time = pacific.ToString(settings.TwelveHour ? (settings.Seconds ? "hh:mm:ss" : "hh:mm") : (settings.Seconds ? "HH:mm:ss" : "HH:mm"), Program.English);
        string suffix = settings.TwelveHour ? pacific.ToString("tt", Program.English) : Program.ZoneLabel(currentUtc);
        if (settings.Compact)
        {
            FillRound(g, new Rectangle(S(10), S(11), S(30), S(32)), 11, Badge);
            Draw(g, "PT", 17, 21, 11, FontStyle.Bold, Accent);
            DrawTime(g, time, 49, 6, settings.Seconds ? 31 : 34);
            Draw(g, suffix, settings.Seconds ? 167 : 135, 23, 10, FontStyle.Bold, Muted);
        }
        else
        {
            DrawSpaced(g, "PACIFIC", 24, 22, 11, 2.1f, Muted);
            FillRound(g, new Rectangle(S(130), S(18), S(39), S(22)), 7, Badge);
            Draw(g, Program.ZoneLabel(currentUtc), 139, 23, 10, FontStyle.Bold, Accent);
            DrawTime(g, time, 20, settings.Seconds ? 54 : 40, settings.Seconds ? 43 : 64);
            DrawAnalog(g, pacific, 206, 80, 16);
            if (settings.TwelveHour) Draw(g, suffix, 196, 103, 10, FontStyle.Bold, Muted);
            using (Pen line = new Pen(Edge, scale)) g.DrawLine(line, S(24), S(123), Width - S(24), S(123));
            string offset = Program.Pacific.GetUtcOffset(new DateTimeOffset(currentUtc)).TotalHours.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            DrawSpaced(g, pacific.ToString("ddd, MMM dd", Program.English).ToUpperInvariant(), 24, 136, 10, 0.8f, Muted);
            Draw(g, "UTC" + offset, 187, 136, 10, FontStyle.Regular, Muted);
        }
    }

    private void ApplyTheme()
    {
        BackColor = Surface;
        optionsButton.BackColor = Surface;
        optionsButton.ForeColor = Muted;
        optionsButton.FlatAppearance.MouseOverBackColor = Badge;
        optionsButton.FlatAppearance.MouseDownBackColor = Edge;
        menu.BackColor = Surface;
        menu.ForeColor = Ink;
        menu.Renderer = new ToolStripProfessionalRenderer(new ClockMenuColors(Surface, Badge, Edge));
        ApplyMenuTheme(menu.Items);
        Invalidate();
    }

    private void ApplyMenuTheme(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = Ink;
            ToolStripMenuItem parent = item as ToolStripMenuItem;
            if (parent != null && parent.HasDropDownItems)
            {
                parent.DropDown.BackColor = Surface;
                parent.DropDown.ForeColor = Ink;
                parent.DropDown.Renderer = menu.Renderer;
                ApplyMenuTheme(parent.DropDownItems);
            }
        }
    }

    private void FillRound(Graphics g, Rectangle rectangle, int radius, Color color)
    {
        using (GraphicsPath path = Rounded(rectangle, S(radius)))
        using (SolidBrush brush = new SolidBrush(color)) g.FillPath(brush, path);
    }

    private void DrawTime(Graphics g, string text, float x, float y, float size)
    {
        using (Font font = new Font("Segoe UI Light", size * scale, FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush brush = new SolidBrush(Ink))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            g.DrawString(text, font, brush, S(x), S(y), format);
    }

    private void DrawSpaced(Graphics g, string text, float x, float y, float size, float spacing, Color color)
    {
        using (Font font = UiFont(size, FontStyle.Regular))
        using (SolidBrush brush = new SolidBrush(color))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        {
            float cursor = S(x);
            foreach (char letter in text)
            {
                string character = letter.ToString();
                g.DrawString(character, font, brush, cursor, S(y), format);
                cursor += g.MeasureString(character, font, int.MaxValue, format).Width + spacing * scale;
            }
        }
    }

    private void DrawAnalog(Graphics g, DateTime time, float x, float y, float radius)
    {
        using (Pen rim = new Pen(Edge, scale)) g.DrawEllipse(rim, S(x - radius), S(y - radius), S(radius * 2), S(radius * 2));
        for (int i = 0; i < 12; i++)
        {
            double angle = i * Math.PI / 6;
            float inner = radius - (i % 3 == 0 ? 5 : 3);
            using (Pen tick = new Pen(i % 3 == 0 ? Muted : Edge, scale))
                g.DrawLine(tick, S(x + inner * (float)Math.Sin(angle)), S(y - inner * (float)Math.Cos(angle)),
                    S(x + (radius - 1) * (float)Math.Sin(angle)), S(y - (radius - 1) * (float)Math.Cos(angle)));
        }
        double hourAngle = (time.Hour % 12 + time.Minute / 60.0) * Math.PI / 6;
        double minuteAngle = time.Minute * Math.PI / 30;
        using (Pen hand = new Pen(Accent, S(2)))
        {
            hand.StartCap = LineCap.Round; hand.EndCap = LineCap.Round;
            g.DrawLine(hand, S(x), S(y), S(x + 9 * (float)Math.Sin(hourAngle)), S(y - 9 * (float)Math.Cos(hourAngle)));
            g.DrawLine(hand, S(x), S(y), S(x + 13 * (float)Math.Sin(minuteAngle)), S(y - 13 * (float)Math.Cos(minuteAngle)));
        }
        using (SolidBrush dot = new SolidBrush(Accent)) g.FillEllipse(dot, S(x - 2), S(y - 2), S(4), S(4));
    }

    private void Draw(Graphics g, string text, float x, float y, float size, FontStyle style, Color color)
    {
        using (Font font = UiFont(size, style))
        using (SolidBrush brush = new SolidBrush(color))
        using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
        {
            format.FormatFlags |= StringFormatFlags.NoWrap;
            g.DrawString(text, font, brush, S(x), S(y), format);
        }
    }

    private void TickClock()
    {
        currentUtc = DateTime.UtcNow;
        DateTime time = TimeZoneInfo.ConvertTimeFromUtc(currentUtc, Program.Pacific);
        string stamp = time.ToString(settings.Seconds ? "yyyyMMddHHmmss" : "yyyyMMddHHmm", Program.English) + Program.ZoneLabel(currentUtc);
        if (stamp != lastStamp)
        {
            lastStamp = stamp;
            AccessibleDescription = time.ToString("yyyy-MM-dd HH:mm:ss", Program.English) + " " + Program.ZoneLabel(currentUtc);
            if (tray != null) tray.Text = "太平洋 " + time.ToString("MM/dd HH:mm", Program.English) + " " + Program.ZoneLabel(currentUtc) + " · 双击显示";
            Invalidate();
        }
        if (Program.Wake != null && Program.Wake.WaitOne(0)) RestoreClock();
        if (Program.Reveal != null && Program.Reveal.WaitOne(0)) ShowLocator();
    }

    private ToolStripMenuItem Toggle(string text, Func<bool> read, Action action)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text);
        item.Click += delegate { action(); Save(); Invalidate(); };
        menu.Opening += delegate { item.Checked = read(); };
        menu.Items.Add(item);
        return item;
    }

    private void BuildMenu()
    {
        menu.Font = new Font("Microsoft YaHei UI", 9f);
        ToolStripMenuItem title = new ToolStripMenuItem("太平洋时钟 · PT") { Enabled = false };
        menu.Items.Add(title);
        menu.Opening += delegate { title.Text = "太平洋 " + TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Program.Pacific).ToString("yyyy-MM-dd HH:mm", Program.English); };
        menu.Items.Add("显示时钟 / 解除鼠标穿透", null, delegate { RestoreClock(); });
        menu.Items.Add("隐藏时钟（保留托盘）", null, delegate { Hide(); });
        menu.Items.Add("普通窗口模式（显示任务栏按钮）", null, delegate { ShowLocator(); });
        menu.Items.Add("恢复悬浮模式（隐藏任务栏按钮）", null, delegate { ShowWidget(); });
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem themes = new ToolStripMenuItem("外观主题");
        string[] themeNames = { "暖白 · Paper", "石墨 · Graphite" };
        string[] themeKeys = { "Paper", "Graphite" };
        for (int i = 0; i < themeNames.Length; i++)
        {
            string key = themeKeys[i];
            ToolStripMenuItem item = new ToolStripMenuItem(themeNames[i]);
            item.Click += delegate { settings.Theme = key; ApplyTheme(); Save(); };
            themes.DropDownOpening += delegate { item.Checked = settings.Theme == key; };
            themes.DropDownItems.Add(item);
        }
        menu.Items.Add(themes);
        ToolStripMenuItem zoomMenu = new ToolStripMenuItem("缩放比例");
        menu.Opening += delegate { zoomMenu.Text = "缩放比例 · " + Math.Round(settings.Zoom * 100) + "%"; };
        foreach (int percent in new int[] { 50, 65, 75, 85, 100, 125, 150, 200 })
        {
            double selectedZoom = percent / 100.0;
            ToolStripMenuItem item = new ToolStripMenuItem(percent + "%");
            item.Click += delegate { settings.Zoom = selectedZoom; ApplySize(); KeepOnScreen(); Save(); Invalidate(); };
            zoomMenu.DropDownOpening += delegate { item.Checked = Math.Abs(settings.Zoom - selectedZoom) < 0.005; };
            zoomMenu.DropDownItems.Add(item);
        }
        zoomMenu.DropDownItems.Add(new ToolStripSeparator());
        zoomMenu.DropDownItems.Add(new ToolStripMenuItem("拖动边缘缩放 · 拖动中间移动") { Enabled = false });
        menu.Items.Add(zoomMenu);
        Toggle("始终置顶", delegate { return settings.Topmost; }, delegate { settings.Topmost = !settings.Topmost; TopMost = settings.Topmost; });
        Toggle("精简模式", delegate { return settings.Compact; }, delegate { settings.Compact = !settings.Compact; ApplySize(); KeepOnScreen(); });
        Toggle("显示秒", delegate { return settings.Seconds; }, delegate { settings.Seconds = !settings.Seconds; ApplySize(); KeepOnScreen(); });
        Toggle("12 小时制", delegate { return settings.TwelveHour; }, delegate { settings.TwelveHour = !settings.TwelveHour; });
        Toggle("鼠标穿透（托盘右键可解除）", delegate { return settings.ClickThrough; }, delegate { settings.ClickThrough = !settings.ClickThrough; ApplyOpacity(); UpdateStyles(); });
        ToolStripMenuItem opacity = new ToolStripMenuItem("透明度");
        double[] levels = { 1, 0.94, 0.75, 0.55 };
        foreach (double level in levels)
        {
            double selected = level;
            ToolStripMenuItem item = new ToolStripMenuItem(((int)Math.Round(level * 100)).ToString() + "%");
            item.Click += delegate { settings.Opacity = selected; ApplyOpacity(); Save(); };
            menu.Opening += delegate { item.Checked = Math.Abs(settings.Opacity - selected) < 0.001; };
            opacity.DropDownItems.Add(item);
        }
        menu.Items.Add(opacity);
        menu.Items.Add("移回屏幕右下角", null, delegate {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - S(20), area.Bottom - Height - S(20));
            RestoreClock();
        });
        menu.Items.Add(new ToolStripSeparator());
        Toggle("开机启动", IsStartupEnabled, delegate { SetStartup(!IsStartupEnabled()); });
        menu.Items.Add("退出", null, delegate { Close(); });
        ApplyTheme();
    }

    private bool IsStartupEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
            return key != null && string.Equals(key.GetValue("PacificDesktopClock") as string, "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase);
    }

    private void SetStartup(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("PacificDesktopClock", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("PacificDesktopClock", false);
            }
        }
        catch (Exception ex) { MessageBox.Show("无法修改开机启动：\n" + ex.Message, Text); }
    }

    private void RestoreClock()
    {
        settings.ClickThrough = false;
        ApplyOpacity();
        UpdateStyles();
        KeepOnScreen();
        Show();
        Save();
    }

    internal void ShowLocator()
    {
        ShowOrdinary(true);
    }

    private void ShowOrdinary(bool highlight)
    {
        locator = true;
        settings.OrdinaryWindow = true;
        settings.ClickThrough = false;
        if (highlight)
        {
            settings.Compact = false;
            settings.Opacity = 1;
            settings.Topmost = true;
        }
        TopMost = settings.Topmost;
        ApplyOpacity();
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        ShowInTaskbar = true;
        ApplySize();
        UpdateStyles();
        if (highlight)
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        }
        else KeepOnScreen();
        Show();
        WindowState = FormWindowState.Normal;
        if (highlight) { Activate(); BringToFront(); }
        Invalidate();
        Save();
    }

    private void ShowWidget()
    {
        locator = false;
        settings.OrdinaryWindow = false;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ApplySize();
        UpdateStyles();
        RestoreClock();
    }

    private void KeepOnScreen() { Location = Program.Clamp(Location, Size, Screen.FromPoint(Location).WorkingArea); }

    private void Save()
    {
        if (preview) return;
        settings.X = Left;
        settings.Y = Top;
        try { Program.SaveSettings(settings); }
        catch (Exception ex)
        {
            if (!saveWarningShown)
            {
                saveWarningShown = true;
                MessageBox.Show("时钟仍可使用，但无法保存设置。请把整个文件夹移到可写位置。\n" + ex.Message, Text);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            resizeEdges = ResizeEdgesAt(e.Location);
            dragging = true;
            dragStart = Cursor.Position;
            windowStart = Location;
            resizeStart = Bounds;
            zoomStart = settings.Zoom;
            Capture = true;
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
        {
            Point delta = new Point(Cursor.Position.X - dragStart.X, Cursor.Position.Y - dragStart.Y);
            if (resizeEdges != 0) ResizeFromDelta(delta);
            else Location = new Point(windowStart.X + delta.X, windowStart.Y + delta.Y);
        }
        else
        {
            int edge = ResizeEdgesAt(e.Location);
            bool horizontal = (edge & 3) != 0, vertical = (edge & 12) != 0;
            Cursor = horizontal && vertical ? ((edge == 5 || edge == 10) ? Cursors.SizeNWSE : Cursors.SizeNESW)
                : horizontal ? Cursors.SizeWE : vertical ? Cursors.SizeNS : Cursors.SizeAll;
        }
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) FinishDrag();
    }

    private static double NormalizeZoom(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 1 : Math.Max(0.5, Math.Min(2, value));
    }

    // A fixed DPI-aware hit target stays usable even at 50% zoom.
    private int ResizeEdgesAt(Point point)
    {
        int grip = Math.Max(5, (int)Math.Round(6 * dpiScale));
        return (point.X < grip ? 1 : point.X >= Width - grip ? 2 : 0)
            | (point.Y < grip ? 4 : point.Y >= Height - grip ? 8 : 0);
    }

    private void ResizeFromDelta(Point delta)
    {
        double x = (resizeEdges & 1) != 0 ? -resizeStart.Width : (resizeEdges & 2) != 0 ? resizeStart.Width : 0;
        double y = (resizeEdges & 4) != 0 ? -resizeStart.Height : (resizeEdges & 8) != 0 ? resizeStart.Height : 0;
        settings.Zoom = NormalizeZoom(zoomStart * (1 + (delta.X * x + delta.Y * y) / (x * x + y * y)));
        ApplySize();
        Location = new Point((resizeEdges & 1) != 0 ? resizeStart.Right - Width : resizeStart.Left,
            (resizeEdges & 4) != 0 ? resizeStart.Bottom - Height : resizeStart.Top);
        Invalidate();
    }

    private void FinishDrag()
    {
        if (!dragging) return;
        dragging = false;
        resizeEdges = 0;
        Capture = false;
        KeepOnScreen();
        Save();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) FinishDrag();
    }

    private void DisplayChanged(object sender, EventArgs e)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { KeepOnScreen(); });
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && !IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { TickClock(); });
    }

    private static Icon MakeIcon()
    {
        using (Bitmap bitmap = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(Color.FromArgb(123, 227, 196), 2.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(pen, 3, 3, 26, 26);
                g.DrawLine(pen, 16, 16, 16, 8);
                g.DrawLine(pen, 16, 16, 23, 20);
            }
            IntPtr handle = bitmap.GetHicon();
            try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    internal void RenderPreview(string path)
    {
        currentUtc = DateTime.UtcNow;
        using (Bitmap bitmap = new Bitmap(Width, Height))
        { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        settings.Compact = true;
        ApplySize();
        using (Bitmap bitmap = new Bitmap(Width, Height))
        { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(Path.GetDirectoryName(path), "compact.png"), System.Drawing.Imaging.ImageFormat.Png); }
        settings.Theme = "Graphite";
        ApplyTheme();
        using (Bitmap bitmap = new Bitmap(Width, Height))
        { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(Path.GetDirectoryName(path), "compact-dark.png"), System.Drawing.Imaging.ImageFormat.Png); }
        foreach (double zoom in new double[] { 0.5, 0.65, 0.75 })
        {
            settings.Zoom = zoom;
            ApplySize();
            using (Bitmap bitmap = new Bitmap(Width, Height))
            { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(Path.GetDirectoryName(path), "compact-" + Math.Round(zoom * 100) + ".png"), System.Drawing.Imaging.ImageFormat.Png); }
        }
        settings.Zoom = 1;
        settings.Compact = false;
        ApplySize();
        using (Bitmap bitmap = new Bitmap(Width, Height))
        { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(Path.GetDirectoryName(path), "dark.png"), System.Drawing.Imaging.ImageFormat.Png); }
    }

    internal static int VerifyWindowBehavior(StringBuilder report)
    {
        int failures = 0;
        Action<bool, string> check = delegate(bool pass, string name)
        {
            if (!pass) failures++;
            report.AppendLine((pass ? "PASS " : "FAIL ") + name);
        };
        ClockSettings value = new ClockSettings { Opacity = 1, ClickThrough = true, Compact = true };
        using (ClockForm form = new ClockForm(value, true))
        {
            check(!value.OrdinaryWindow && value.Compact, "fresh install defaults to compact floating mode");
            int style = GetWindowLong(form.Handle, -20);
            check((style & WS_EX_NOACTIVATE) != 0 && (style & WS_EX_TOOLWINDOW) != 0 && !form.ShowInTaskbar,
                "nonactivating tool window / no taskbar button");
            check((style & WS_EX_TRANSPARENT) != 0 && (style & 0x80000) != 0,
                "click-through remains layered at 100% selected opacity");
            ToolStripMenuItem passthrough = null;
            ToolStripMenuItem compact = null;
            foreach (ToolStripItem item in form.menu.Items)
            {
                if (item.Text.StartsWith("鼠标穿透")) passthrough = (ToolStripMenuItem)item;
                if (item.Text == "精简模式") compact = (ToolStripMenuItem)item;
            }
            passthrough.PerformClick();
            style = GetWindowLong(form.Handle, -20);
            check(!value.ClickThrough && (style & WS_EX_TRANSPARENT) == 0 && form.Opacity == 1,
                "menu disables click-through and restores full opacity");
            passthrough.PerformClick();
            style = GetWindowLong(form.Handle, -20);
            check(value.ClickThrough && (style & WS_EX_TRANSPARENT) != 0 && (style & 0x80000) != 0,
                "menu re-enables layered click-through");
            compact.PerformClick();
            check(!value.Compact && form.Width == form.S(248) && form.Height == form.S(160),
                "compact menu switches actual window dimensions");
            form.ShowLocator();
            style = GetWindowLong(form.Handle, -20);
            check(form.Visible && form.ShowInTaskbar && (style & (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW)) == 0 && (style & 0x40000) != 0,
                "locator mode is a visible activatable taskbar window");
            form.ShowWidget();
            style = GetWindowLong(form.Handle, -20);
            check(!form.ShowInTaskbar && (style & WS_EX_NOACTIVATE) != 0 && (style & WS_EX_TOOLWINDOW) != 0,
                "locator returns to nonactivating floating mode");
            value.Compact = true;
            value.Zoom = 0.5;
            form.ApplySize();
            check(form.Width == (int)Math.Round(85 * form.dpiScale) && form.Height == (int)Math.Round(27 * form.dpiScale),
                "50 percent compact scales both window dimensions");
            check(form.ResizeEdgesAt(new Point(form.Width - 2, form.Height / 2)) == 2
                && form.ResizeEdgesAt(new Point(form.Width / 2, form.Height / 2)) == 0,
                "small clock preserves distinct resize and move hit targets");
            value.Zoom = 1;
            form.ApplySize();
            Rectangle initialBounds = form.Bounds;
            form.resizeStart = initialBounds;
            form.zoomStart = 1;
            form.resizeEdges = 2;
            form.ResizeFromDelta(new Point(-form.Width / 4, 0));
            check(Math.Abs(value.Zoom - 0.75) < 0.01 && form.Left == initialBounds.Left,
                "right-edge drag shrinks proportionally with left edge anchored");
            initialBounds = form.Bounds;
            form.resizeStart = initialBounds;
            form.zoomStart = value.Zoom;
            form.resizeEdges = 5;
            form.ResizeFromDelta(new Point(-form.Width, -form.Height));
            check(Math.Abs(value.Zoom - 1.5) < 0.02 && form.Right == initialBounds.Right && form.Bottom == initialBounds.Bottom,
                "top-left corner drag keeps opposite corner fixed");
            form.ResizeFromDelta(new Point(-99999, -99999));
            check(value.Zoom == 2, "drag zoom upper limit");
            form.ResizeFromDelta(new Point(99999, 99999));
            check(value.Zoom == 0.5 && NormalizeZoom(double.NaN) == 1 && NormalizeZoom(double.PositiveInfinity) == 1,
                "drag zoom lower limit and corrupt scale recovery");
            value.Seconds = true;
            form.ApplySize();
            check(form.Width == form.S(202) && value.Zoom == 0.5, "seconds layout retains zoom");
            using (StringReader oldSettings = new StringReader("<ClockSettings><Compact>true</Compact></ClockSettings>"))
                check(((ClockSettings)new XmlSerializer(typeof(ClockSettings)).Deserialize(oldSettings)).Zoom == 1,
                    "settings from older versions retain 100 percent scale");
            value.Compact = false;
            form.ShowOrdinary(false);
            foreach (double zoom in new double[] { 0.5, 0.65, 1, 1.5, 2, 0.5, 1 })
            {
                value.Zoom = zoom;
                form.ApplySize();
                form.optionsButton.PerformLayout();
                form.Refresh();
                Application.DoEvents();
                using (Bitmap rendered = new Bitmap(form.Width, form.Height))
                    form.DrawToBitmap(rendered, form.ClientRectangle);
            }
            check(!form.optionsButton.IsDisposed && form.optionsButton.Visible,
                "live full-card button layout and painting after repeated zoom changes");
        }
        return failures;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Save();
        base.OnFormClosed(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!preview)
            {
                SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                SystemEvents.PowerModeChanged -= PowerChanged;
            }
            timer.Dispose();
            tips.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (clockIcon != null) clockIcon.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class ClockMenuColors : ProfessionalColorTable
{
    private readonly Color surface, hover, border;
    internal ClockMenuColors(Color background, Color selected, Color edge)
    { surface = background; hover = selected; border = edge; UseSystemColors = false; }
    public override Color ToolStripDropDownBackground { get { return surface; } }
    public override Color ImageMarginGradientBegin { get { return surface; } }
    public override Color ImageMarginGradientMiddle { get { return surface; } }
    public override Color ImageMarginGradientEnd { get { return surface; } }
    public override Color MenuItemSelected { get { return hover; } }
    public override Color MenuItemSelectedGradientBegin { get { return hover; } }
    public override Color MenuItemSelectedGradientEnd { get { return hover; } }
    public override Color MenuItemBorder { get { return border; } }
    public override Color MenuBorder { get { return border; } }
    public override Color SeparatorDark { get { return border; } }
    public override Color SeparatorLight { get { return surface; } }
}
