using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CrosshairApp.Models;
using CrosshairApp.Services;
using Forms = System.Windows.Forms;

namespace CrosshairApp.Views;

public sealed class OverlayWindow : Window
{
    private readonly CrosshairVisual visual = new() { PhysicalPixels = true, IsHitTestVisible = false };
    private readonly DispatcherTimer effects = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private IntPtr handle;
    private Profile? current;
    private bool enabled = true, captureExcluded;
    private int centerX, centerY;
    public string MonitorName { get; set; } = "";
    public string CaptureStatus => captureExcluded ? "" : "Dynamic color: sampling around crosshair (capture exclusion unavailable).";
    public OverlayWindow()
    {
        Title = "AIMORA Overlay"; Width = Height = 640; AllowsTransparency = true; WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        IsHitTestVisible = false; ResizeMode = ResizeMode.NoResize; Content = visual;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(handle, Native.GwlExStyle, Native.GetWindowLong(handle, Native.GwlExStyle) | Native.OverlayStyles);
            HwndSource.FromHwnd(handle).AddHook(Hook);
            // Windows 10 2004+: exclude ourselves so adaptive color samples the game, not our own drawing.
            captureExcluded = Native.SetWindowDisplayAffinity(handle, 0x11) && Environment.OSVersion.Version.Build >= 19041;
            Reposition();
        };
        effects.Tick += (_, _) => Animate();
        Closed += (_, _) => { effects.Stop(); if (current != null) current.PropertyChanged -= Changed; };
    }
    private IntPtr Hook(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (msg == 0x84) { handled = true; return new IntPtr(-1); } // HTTRANSPARENT
        if (msg is 0x2E0 or 0x7E) Dispatcher.BeginInvoke(new Action(Reposition));
        return IntPtr.Zero;
    }
    public void Apply(Profile p)
    {
        if (current != null) current.PropertyChanged -= Changed;
        current = p; visual.Profile = p; p.PropertyChanged += Changed; Changed(null, new(null));
    }
    private void Changed(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "OffsetX" or "OffsetY") Reposition();
        visual.DynamicColor = null; visual.PulseOpacity = 1;
        if (enabled && current is { } p && (p.DynamicColor || (p.Pulse && !Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation))) effects.Start(); else effects.Stop();
    }
    public void Toggle() => SetVisible(!enabled);
    public void SetVisible(bool value)
    {
        enabled = value;
        if (value) { Show(); Reposition(); } else Hide();
        Changed(null, new(null));
    }
    public void Reposition()
    {
        if (handle == IntPtr.Zero) return;
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == MonitorName) ?? Forms.Screen.PrimaryScreen!;
        var r = screen.Bounds;
        int Offset(double value) => double.IsFinite(value) ? (int)Math.Round(Math.Clamp(value, -8000, 8000)) : 0;
        centerX = r.Left + r.Width / 2 + Offset(current?.OffsetX ?? 0);
        centerY = r.Top + r.Height / 2 + Offset(current?.OffsetY ?? 0);
        Native.SetWindowPos(handle, new IntPtr(-1), centerX - 320, centerY - 320, 640, 640, 0x10 | 0x400);
        visual.InvalidateVisual();
    }
    private void Animate()
    {
        if (current == null) return;
        if (current.Pulse && !Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation) visual.PulseOpacity = .65 + .35 * Math.Sin(Environment.TickCount64 / 250.0);
        else { visual.PulseOpacity = 1; if (!current.DynamicColor) effects.Stop(); }
        if (current.DynamicColor)
        {
            var dc = Native.GetDC(IntPtr.Zero);
            if (dc != IntPtr.Zero)
            {
                try
                {
                    // On older Windows sample outside the overlay, avoiding color feedback.
                    uint c = Native.GetPixel(dc, centerX + (captureExcluded ? 0 : 324), centerY);
                    if (c != uint.MaxValue)
                    {
                        double luma = .2126 * (c & 255) + .7152 * ((c >> 8) & 255) + .0722 * ((c >> 16) & 255);
                        visual.DynamicColor = luma > 140 ? Colors.Black : Colors.White;
                    }
                }
                finally { Native.ReleaseDC(IntPtr.Zero, dc); }
            }
        }
        visual.InvalidateVisual();
    }
}

