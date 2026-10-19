using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CrosshairApp.Controls;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
namespace CrosshairApp.Views;
public sealed class TrayPopupWindow : Window
{
    public TrayPopupWindow(EditorViewModel vm, OverlayWindow overlay, Action open, Action exit)
    {
        SetResourceReference(TitleProperty, "S.AppName"); Width = 280; MaxHeight = 440; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        var frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(12) }; frame.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); frame.SetResourceReference(Border.BackgroundProperty, "CanvasBrush"); Content = frame;
        var root = new StackPanel(); frame.Child = root; root.Children.Add(new TrackedLabel("Profiles"));
        var list = new StackPanel();
        if (vm.Profiles.Count == 0) list.Children.Add(Ui.Text("EmptyProfiles", "MutedBrush"));
        foreach (var p in vm.Profiles)
        {
            var b = new Button { Content = p.Name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            if (p == vm.Selected) b.SetResourceReference(BorderBrushProperty, "AccentBrush"); b.Click += (_, _) => { vm.Selected = p; Close(); }; list.Children.Add(b);
        }
        root.Children.Add(new ScrollViewer { Content = list, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(Ui.Line()); root.Children.Add(Ui.Button("Toggle", () => { overlay.Toggle(); Close(); }));
        root.Children.Add(Ui.Button("ShowEditor", () => { Close(); open(); })); root.Children.Add(Ui.Button("Exit", () => { Close(); exit(); }));
        Deactivated += (_, _) => Close(); KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Loaded += (_, _) =>
        {
            var cursor = System.Windows.Forms.Cursor.Position; var source = PresentationSource.FromVisual(this);
            var point = source?.CompositionTarget?.TransformFromDevice.Transform(new Point(cursor.X, cursor.Y)) ?? new Point(cursor.X, cursor.Y);
            var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
            var topLeft = source?.CompositionTarget?.TransformFromDevice.Transform(new Point(area.Left, area.Top)) ?? new Point(area.Left, area.Top);
            var bottomRight = source?.CompositionTarget?.TransformFromDevice.Transform(new Point(area.Right, area.Bottom)) ?? new Point(area.Right, area.Bottom);
            Left = Math.Clamp(point.X - Width, topLeft.X, Math.Max(topLeft.X, bottomRight.X - Width)); Top = Math.Clamp(point.Y - ActualHeight, topLeft.Y, Math.Max(topLeft.Y, bottomRight.Y - ActualHeight)); Activate();
        };
    }
}

