using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairApp.Services;

namespace CrosshairApp.Controls;

public sealed class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(string), typeof(ColorPicker), new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorPicker)d).Sync()));
    public string Color { get => (string)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public event Action? InteractionStarted, InteractionCompleted;
    public event Action<string>? ColorCommitted;
    private readonly ColorPlane sv = new(false), hue = new(true);
    private readonly TextBox hex = new() { MaxLength = 7 };
    private readonly TextBlock error = Ui.Text("", "MutedBrush");
    private readonly WrapPanel recent = new();
    private double h, s = 1, v = 1;
    private bool syncing, typing;
    public ColorPicker()
    {
        var root = new StackPanel(); Content = root;
        var planes = new Grid { Height = 122, Margin = new Thickness(0, 4, 0, 8) };
        planes.ColumnDefinitions.Add(new()); planes.ColumnDefinitions.Add(new() { Width = new GridLength(24) });
        sv.Margin = new Thickness(0, 0, 8, 0); planes.Children.Add(sv); Grid.SetColumn(hue, 1); planes.Children.Add(hue); root.Children.Add(planes);
        sv.ToolTip = "S / V"; hue.ToolTip = "H";
        sv.Edited += (x, y) => { s = x; v = 1 - y; Emit(); };
        hue.Edited += (_, y) => { h = y * 359.99; Emit(); };
        foreach (var p in new[] { sv, hue }) { p.Started += () => InteractionStarted?.Invoke(); p.Completed += Finish; }
        var row = new DockPanel(); root.Children.Add(row);
        var pick = Ui.IconButton("Pipette", "Eyedropper", PickScreen); DockPanel.SetDock(pick, Dock.Right); pick.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(pick);
        hex.SetResourceReference(TextBox.FontFamilyProperty, "NumberFont"); row.Children.Add(hex);
        System.Windows.Automation.AutomationProperties.SetName(hex, Appearance.Text("Hex"));
        hex.GotKeyboardFocus += (_, _) => { if (!typing) { typing = true; InteractionStarted?.Invoke(); } };
        hex.LostKeyboardFocus += (_, _) => { if (typing) { typing = false; Sync(); Finish(); } };
        hex.TextChanged += (_, _) =>
        {
            if (syncing) return;
            if (System.Text.RegularExpressions.Regex.IsMatch(hex.Text, "^#[0-9a-fA-F]{6}$")) { error.Text = ""; SetCurrentValue(ColorProperty, hex.Text.ToUpperInvariant()); }
            else error.Text = Appearance.Text("ColorInvalid");
        };
        error.MinHeight = 18; root.Children.Add(error); root.Children.Add(new TrackedLabel("Recent")); root.Children.Add(recent); Sync();
    }
    public void SetRecent(IEnumerable<string> colors)
    {
        recent.Children.Clear();
        foreach (var c in colors.Take(8))
        {
            try
            {
                var patch = new Border { Width = 18, Height = 18, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)) };
                var b = new Button { Content = patch, Width = 30, Height = 30, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(4), ToolTip = c };
                System.Windows.Automation.AutomationProperties.SetName(b, c);
                b.Click += (_, _) => { InteractionStarted?.Invoke(); SetCurrentValue(ColorProperty, c); Finish(); }; recent.Children.Add(b);
            }
            catch { }
        }
    }
    private void Finish() { ColorCommitted?.Invoke(Color); InteractionCompleted?.Invoke(); }
    private void Emit() { var c = Hsv(h, s, v); SetCurrentValue(ColorProperty, $"#{c.R:X2}{c.G:X2}{c.B:X2}"); }
    private void Sync()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(Color); double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            if (delta > 0) h = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) * 60 + 360) % 360;
            s = max == 0 ? 0 : delta / max; v = max;
            sv.Hue = h; sv.X = s; sv.Y = 1 - v; hue.Y = h / 360; sv.InvalidateVisual(); hue.InvalidateVisual();
            if (!typing) { syncing = true; hex.Text = Color; syncing = false; } error.Text = "";
        }
        catch { syncing = false; }
    }
    public static Color Hsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
        var (r, g, b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return System.Windows.Media.Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    private void PickScreen()
    {
        InteractionStarted?.Invoke();
        var capture = new ScreenPicker(); capture.Picked += c => SetCurrentValue(ColorProperty, $"#{c.R:X2}{c.G:X2}{c.B:X2}"); capture.Closed += (_, _) => Finish(); capture.Show();
    }
}

internal sealed class ColorPlane : FrameworkElement
{
    public double Hue, X, Y;
    private readonly bool strip;
    public event Action<double, double>? Edited;
    public event Action? Started, Completed;
    private BitmapSource? bitmap;
    private int oldHue = -1;
    public ColorPlane(bool strip)
    {
        this.strip = strip; Focusable = true;
        System.Windows.Automation.AutomationProperties.SetName(this, strip ? "Hue" : "Saturation / value");
        MouseLeftButtonDown += (_, e) => { Focus(); Started?.Invoke(); CaptureMouse(); Update(e.GetPosition(this)); e.Handled = true; };
        MouseMove += (_, e) => { if (IsMouseCaptured) Update(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, _) => { if (IsMouseCaptured) ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => Completed?.Invoke();
        KeyDown += (_, e) =>
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? .001 : .01;
            if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
            Started?.Invoke(); X = Math.Clamp(X + (e.Key == Key.Right ? step : e.Key == Key.Left ? -step : 0), 0, 1); Y = Math.Clamp(Y + (e.Key == Key.Down ? step : e.Key == Key.Up ? -step : 0), 0, 1); Edited?.Invoke(X, Y); Completed?.Invoke(); e.Handled = true;
        };
        GotKeyboardFocus += (_, _) => InvalidateVisual(); LostKeyboardFocus += (_, _) => InvalidateVisual();
    }
    private void Update(Point p) { X = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1); Y = Math.Clamp(p.Y / Math.Max(1, ActualHeight), 0, 1); Edited?.Invoke(X, Y); }
    protected override void OnRender(DrawingContext dc)
    {
        if (bitmap == null || (!strip && oldHue != (int)Hue))
        {
            oldHue = (int)Hue; const int size = 128; var pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { var c = ColorPicker.Hsv(strip ? y / 127d * 359.99 : Hue, strip ? 1 : x / 127d, strip ? 1 : 1 - y / 127d); int i = (y * size + x) * 4; pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R; pixels[i + 3] = 255; }
            bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); bitmap.Freeze();
        }
        dc.DrawImage(bitmap, new Rect(RenderSize));
        if (strip) { dc.DrawRectangle(null, new Pen(Brushes.Black, 3), new Rect(0, Y * Math.Max(0, ActualHeight - 4), ActualWidth, 4)); dc.DrawRectangle(null, new Pen(Brushes.White, 1), new Rect(0, Y * Math.Max(0, ActualHeight - 4), ActualWidth, 4)); }
        else { var p = new Point(X * ActualWidth, Y * ActualHeight); dc.DrawEllipse(null, new Pen(Brushes.Black, 3), p, 4, 4); dc.DrawEllipse(null, new Pen(Brushes.White, 1), p, 4, 4); }
        if (IsKeyboardFocused) dc.DrawRectangle(null, new Pen((Brush)FindResource("FocusBrush"), 2), new Rect(1, 1, Math.Max(0, ActualWidth - 2), Math.Max(0, ActualHeight - 2)));
    }
}

internal sealed class ScreenPicker : Window
{
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    private bool capturing;
    public event Action<Color>? Picked;
    public ScreenPicker()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)); Topmost = true; ShowInTaskbar = false;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight; Cursor = Cursors.Cross;
        var text = Ui.Text("Picking"); var label = new Border { Child = text, Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top }; label.SetResourceReference(Border.BackgroundProperty, "PanelBrush"); Content = label;
        SourceInitialized += (_, _) => Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, _) =>
        {
            capturing = true; Hide(); DwmFlush();
            if (GetCursorPos(out var p))
            {
                var dc = Native.GetDC(IntPtr.Zero);
                try { uint c = Native.GetPixel(dc, p.X, p.Y); if (c != uint.MaxValue) Picked?.Invoke(System.Windows.Media.Color.FromRgb((byte)(c & 255), (byte)((c >> 8) & 255), (byte)((c >> 16) & 255))); }
                finally { Native.ReleaseDC(IntPtr.Zero, dc); }
            }
            Close();
        };
        Deactivated += (_, _) => { if (!capturing && IsVisible) Close(); };
    }
}

