using System.Windows;
using System.Windows.Media;
namespace CrosshairApp.Views;
// Static alignment grid; no animation or idle render loop.
public sealed class PreviewSurface : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen((Brush)FindResource("GridBrush"), .6);
        for (double x = ActualWidth / 2 % 24; x < ActualWidth; x += 24) dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        for (double y = ActualHeight / 2 % 24; y < ActualHeight; y += 24) dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        dc.DrawEllipse(null, new Pen((Brush)FindResource("LineBrush"), 1) { DashStyle = DashStyles.Dash }, new Point(ActualWidth / 2, ActualHeight / 2), 66, 66);
    }
}
