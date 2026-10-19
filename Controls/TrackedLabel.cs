using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CrosshairApp.Services;

namespace CrosshairApp.Controls;

// WPF TextBlock has no letter-spacing; draw labels with a measured +0.08em advance.
public sealed class TrackedLabel : FrameworkElement
{
    public string Text { get; set; } = "";
    public TrackedLabel(string key) { Text = Appearance.Text(key).ToUpperInvariant(); Height = 18; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext dc)
    {
        var font = new Typeface((FontFamily)FindResource("LabelFont"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var brush = (Brush)FindResource("MutedBrush"); double size = (double)FindResource("LabelSize"), x = 0;
        foreach (var c in Text)
        {
            var f = new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            if (x + f.Width > ActualWidth) break;
            dc.DrawText(f, new Point(x, 0)); x += f.WidthIncludingTrailingWhitespace + size * (double)FindResource("LabelTracking");
        }
    }
}

