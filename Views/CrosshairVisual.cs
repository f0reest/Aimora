using System;
using System.Windows;
using System.Windows.Media;
using CrosshairApp.Models;
using CrosshairApp.Services;

namespace CrosshairApp.Views;

public sealed class CrosshairVisual : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile), typeof(Profile), typeof(CrosshairVisual), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public Profile? Profile { get => (Profile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var v = (CrosshairVisual)d;
        if (e.OldValue is Profile old) old.PropertyChanged -= v.OnProfileChanged;
        if (e.NewValue is Profile p) p.PropertyChanged += v.OnProfileChanged;
        v.cachedAsset = null;
    }
    private void OnProfileChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName is "Asset" or "AssetType") cachedAsset = null; InvalidateVisual(); }
    private ImageSource? cachedAsset;
    public Color? DynamicColor { get; set; }
    public double PulseOpacity { get; set; } = 1;
    // Overlay geometry uses physical pixels; editor thumbnails use ordinary WPF DIPs.
    public bool PhysicalPixels { get; set; }
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(CrosshairVisual), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public static readonly DependencyProperty RevealProgressProperty = DependencyProperty.Register(nameof(RevealProgress), typeof(double), typeof(CrosshairVisual), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double RevealProgress { get => (double)GetValue(RevealProgressProperty); set => SetValue(RevealProgressProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var p = Profile; if (p == null) return;
        double scale = (PhysicalPixels ? 1 / VisualTreeHelper.GetDpi(this).DpiScaleX : 1) * Zoom;
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        // A 1 px stroke centered on an integer coordinate straddles two device
        // pixels in WPF and looks grey. Move odd integer strokes by half a pixel
        // so pure white remains crisp in thumbnails and on the overlay.
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double deviceScale = scale * dpi;
        double deviceStroke = p.Thickness * deviceScale;
        double pixelSnap = 0;
        if (Math.Abs(deviceStroke - Math.Round(deviceStroke)) < .001)
        {
            double targetFraction = ((int)Math.Round(deviceStroke) & 1) == 1 ? .5 : 0;
            double centerFraction = ActualWidth / 2 * dpi % 1;
            pixelSnap = (targetFraction - centerFraction) / deviceScale;
        }
        dc.PushTransform(new TranslateTransform(pixelSnap, pixelSnap));
        dc.PushTransform(new RotateTransform(p.Rotation));
        dc.PushOpacity(Math.Clamp(p.Opacity * PulseOpacity, 0, 1));
        double extent = Math.Max(p.Gap + p.Length + p.Thickness + p.OutlineThickness * 2, Math.Max(p.ImageSize / 2, p.DotSize / 2)) + 4;
        dc.PushClip(new RectangleGeometry(new Rect(-extent, -extent, extent * 2 * Math.Clamp(RevealProgress, 0, 1), extent * 2)));
        Brush BrushFor(string text, Color fallback) { try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(text)); } catch { return new SolidColorBrush(fallback); } }
        var color = DynamicColor.HasValue && p.DynamicColor ? new SolidColorBrush(DynamicColor.Value) : BrushFor(p.Color, Colors.Lime);
        var border = BrushFor(p.OutlineColor, Colors.Black);
        void Draw(Brush brush, double extra)
        {
            var pen = new Pen(brush, p.Thickness + extra * 2);
            void Line(double x1, double y1, double x2, double y2) => dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
            double g = p.Gap, e = g + p.Length;
            switch (p.Style)
            {
                case CrosshairStyle.Cross: case CrosshairStyle.CrossDot: case CrosshairStyle.TShape: case CrosshairStyle.XShape:
                    if (p.Style == CrosshairStyle.XShape) dc.PushTransform(new RotateTransform(45));
                    Line(-e, 0, -g, 0); Line(g, 0, e, 0); Line(0, g, 0, e);
                    if (p.Style != CrosshairStyle.TShape) Line(0, -e, 0, -g);
                    if (p.Style == CrosshairStyle.XShape) dc.Pop();
                    break;
                case CrosshairStyle.Circle: dc.DrawEllipse(null, pen, new Point(), e, e); break;
                case CrosshairStyle.Square: dc.DrawRectangle(null, pen, new Rect(-e, -e, e * 2, e * 2)); break;
                case CrosshairStyle.Chevron: Line(-p.Length, p.Length / 2, 0, -p.Length / 2); Line(0, -p.Length / 2, p.Length, p.Length / 2); break;
            }
            if (p.Style is CrosshairStyle.Dot or CrosshairStyle.CrossDot && p.DotSize > 0)
                dc.DrawEllipse(brush, null, new Point(), p.DotSize / 2 + extra, p.DotSize / 2 + extra);
        }
        if (p.Style == CrosshairStyle.Custom && p.Asset.Length > 0)
        {
            try
            {
                cachedAsset ??= AssetLoader.Load(p);
                double ratio = cachedAsset.Width / cachedAsset.Height;
                double w = ratio >= 1 ? p.ImageSize : p.ImageSize * ratio, h = ratio >= 1 ? p.ImageSize / ratio : p.ImageSize;
                dc.DrawImage(cachedAsset, new Rect(-w / 2, -h / 2, w, h));
            }
            catch { /* Invalid imports are rejected before application; keep rendering resilient. */ }
        }
        else { if (p.Outline) Draw(border, p.OutlineThickness); Draw(color, 0); }
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }
}

