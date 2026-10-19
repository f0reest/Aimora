using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CrosshairApp.Models;
using CrosshairApp.Services;

namespace CrosshairApp.Views;

public sealed class StageView : Grid, IDisposable
{
    private readonly Image background = new() { Stretch = Stretch.UniformToFill };
    private readonly RulerLayer ruler = new();
    public CrosshairVisual Crosshair { get; } = new();
    private readonly DispatcherTimer pulse = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private Profile? profile;
    public string BackgroundKind { get; private set; } = "Concrete";
    public StageView()
    {
        ClipToBounds = true; Children.Add(background); Children.Add(ruler); Children.Add(Crosshair);
        Background = Brushes.Transparent;
        pulse.Tick += (_, _) => { Crosshair.PulseOpacity = .65 + .35 * Math.Sin(Environment.TickCount64 / 250d); Crosshair.InvalidateVisual(); };
        IsVisibleChanged += (_, _) => RefreshEffects();
        SetBackground("Concrete"); Zoom = 4;
    }
    public double Zoom { get => Crosshair.Zoom; set { Crosshair.Zoom = value; ruler.Zoom = value; ruler.InvalidateVisual(); } }
    public void SetBackground(string key, BitmapSource? custom = null) { BackgroundKind = key; background.Source = custom ?? TestTextures.Get(key); RefreshEffects(); }
    public void Apply(Profile p, bool animate = false)
    {
        if (profile != null) profile.PropertyChanged -= Changed;
        profile = p; profile.PropertyChanged += Changed; Crosshair.Profile = p; RefreshEffects();
        Crosshair.BeginAnimation(CrosshairVisual.RevealProgressProperty, null);
        if (animate && !Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation)
            Crosshair.BeginAnimation(CrosshairVisual.RevealProgressProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop });
    }
    private void Changed(object? s, PropertyChangedEventArgs e) => RefreshEffects();
    private void RefreshEffects()
    {
        if (profile?.Pulse == true && IsVisible && !Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation) pulse.Start(); else { pulse.Stop(); Crosshair.PulseOpacity = 1; }
        Crosshair.DynamicColor = null;
        if (profile?.DynamicColor == true && background.Source is BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4]; converted.CopyPixels(new Int32Rect(source.PixelWidth / 2, source.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Crosshair.DynamicColor = .2126 * pixel[2] + .7152 * pixel[1] + .0722 * pixel[0] > 140 ? Colors.Black : Colors.White;
        }
        Crosshair.InvalidateVisual();
    }
    public void Dispose() { pulse.Stop(); if (profile != null) profile.PropertyChanged -= Changed; Crosshair.Profile = null; }
}

internal sealed class RulerLayer : FrameworkElement
{
    public double Zoom = 4;
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w < 40 || h < 40) return;
        var ink = (Brush)FindResource("TextBrush"); var surface = (Brush)FindResource("CanvasBrush");
        var font = new Typeface((FontFamily)FindResource("NumberFont"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        dc.PushOpacity(.35);
        var thin = new Pen(ink, .5); thin.DashStyle = DashStyles.Dash;
        dc.DrawLine(thin, new Point(20, h / 2), new Point(w, h / 2)); dc.DrawLine(thin, new Point(w / 2, 20), new Point(w / 2, h));
        dc.DrawEllipse(null, thin, new Point(w / 2, h / 2), 76, 76); dc.Pop();
        dc.DrawRectangle(surface, null, new Rect(0, 0, w, 20)); dc.DrawRectangle(surface, null, new Rect(0, 0, 24, h));
        var mark = new Pen((Brush)FindResource("SecondaryBrush"), 1);
        double spacing = Zoom * 10; while (spacing < 40) spacing *= 2;
        for (double x = w / 2 % spacing; x < w; x += spacing)
        {
            dc.DrawLine(mark, new Point(x, 14), new Point(x, 20));
            var f = new FormattedText(Math.Round((x - w / 2) / Zoom).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, 11, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(f, new Point(x + 3, 1));
        }
        for (double y = h / 2 % spacing; y < h; y += spacing) dc.DrawLine(mark, new Point(16, y), new Point(24, y));
    }
}

