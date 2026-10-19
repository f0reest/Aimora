using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CrosshairApp.Services;

namespace CrosshairApp.Controls;

public static class Ui
{
    public static TextBlock Text(string key, string brush = "TextBrush", bool heading = false)
    {
        var t = new TextBlock { Text = Appearance.Text(key), TextWrapping = TextWrapping.Wrap };
        t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        t.SetResourceReference(TextBlock.FontSizeProperty, heading ? "HeadingSize" : "BodySize");
        t.SetResourceReference(TextBlock.FontFamilyProperty, heading ? "LabelFont" : "BodyFont"); return t;
    }
    public static Path Icon(string key)
    {
        var p = new Path { Data = (Geometry)Application.Current.FindResource("Icon." + key), Width = 16, Height = 16, StrokeThickness = 1.5, Stretch = Stretch.None, StrokeStartLineCap = PenLineCap.Square, StrokeEndLineCap = PenLineCap.Square };
        p.SetResourceReference(Shape.StrokeProperty, "TextBrush"); p.SetResourceReference(FrameworkElement.WidthProperty, "IconSize"); p.SetResourceReference(FrameworkElement.HeightProperty, "IconSize"); p.SetResourceReference(Shape.StrokeThicknessProperty, "IconStroke"); return p;
    }
    public static Button IconButton(string icon, string tooltip, Action click)
    {
        var b = new Button { Content = Icon(icon), Width = 32, Height = 32, ToolTip = Appearance.Text(tooltip), Padding = new Thickness(6) };
        System.Windows.Automation.AutomationProperties.SetName(b, Appearance.Text(tooltip));
        b.Click += (_, _) => click(); return b;
    }
    public static Button Button(string key, Action click, bool primary = false)
    {
        var b = new Button { Content = Appearance.Text(key) };
        if (primary) b.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        b.Click += (_, _) => click(); return b;
    }
    public static Border Line() { var b = new Border { Height = 1, Margin = new Thickness(0, 12, 0, 12) }; b.SetResourceReference(Border.BackgroundProperty, "LineBrush"); return b; }
}

