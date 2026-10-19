using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shell;
using CrosshairApp.Controls;
using CrosshairApp.Models;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
namespace CrosshairApp.Views;
public sealed class MiniHudWindow : Window
{
    private readonly EditorViewModel vm;
    private CrosshairVisual? preview;
    public MiniHudWindow(EditorViewModel vm)
    {
        this.vm = vm; DataContext = vm; SetResourceReference(TitleProperty, "S.AppName"); SetResourceReference(WidthProperty, "HudWidth"); SetResourceReference(HeightProperty, "HudHeight"); ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
        Topmost = true; ShowActivated = false; ShowInTaskbar = false; Style = (Style)Application.Current.FindResource(typeof(Window));
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 32, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        Build(); vm.SelectionChanged += Selected; Appearance.Changed += Build; vm.PreferencesChanged += Build;
        Closed += (_, _) => { vm.SelectionChanged -= Selected; Appearance.Changed -= Build; vm.PreferencesChanged -= Build; if (preview != null) preview.Profile = null; };
    }
    private void Selected(Profile p) { if (preview != null) preview.Profile = p; }
    private void Build()
    {
        if (preview != null) preview.Profile = null;
        var frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(10) }; frame.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); frame.SetResourceReference(Border.BackgroundProperty, "CanvasBrush");
        var root = new DockPanel(); frame.Child = root; Content = frame;
        var heading = new DockPanel(); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var close = Ui.IconButton("Close", "Close", Close); WindowChrome.SetIsHitTestVisibleInChrome(close, true); DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close); heading.Children.Add(new TrackedLabel("MiniHud") { VerticalAlignment = VerticalAlignment.Center });
        var status = Ui.Text("", "MutedBrush"); status.Text = "Ctrl + Alt + F" + vm.Settings.ToggleKey + " / " + Appearance.Text(((App)Application.Current).HotkeysAvailable ? "On" : "Off"); status.SetResourceReference(TextBlock.FontFamilyProperty, "NumberFont"); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var name = Ui.Text("", "TextBrush"); name.SetBinding(TextBlock.TextProperty, new Binding("Selected.Name")); name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap; DockPanel.SetDock(name, Dock.Bottom); root.Children.Add(name);
        preview = new CrosshairVisual { Zoom = 2, Profile = vm.Selected }; root.Children.Add(preview);
    }
}



