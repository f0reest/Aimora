using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using CrosshairApp.Controls;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
namespace CrosshairApp.Views;
public sealed class OnboardingWindow : Window
{
    private readonly EditorWindow editor;
    private readonly EditorViewModel vm;
    private int step; private Func<bool>? applyKeys;
    public OnboardingWindow(EditorWindow editor, EditorViewModel vm)
    {
        this.editor = editor; this.vm = vm; SetResourceReference(TitleProperty, "S.AppName"); Width = 460; Height = 440; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)Application.Current.FindResource(typeof(Window)); WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 40, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false }); Build();
    }
    public void GoToStep(int value) { step = Math.Clamp(value, 0, 2); Build(); }
    private void Build()
    {
        var frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(24) }; frame.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); frame.SetResourceReference(Border.BackgroundProperty, "CanvasBrush"); Content = frame;
        var root = new DockPanel(); frame.Child = root;
        var header = new DockPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var close = Ui.IconButton("Close", "Close", Close); WindowChrome.SetIsHitTestVisibleInChrome(close, true); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close); header.Children.Add(new TrackedLabel("Onboarding"));
        var lines = new UniformGrid { Columns = 3, Margin = new Thickness(0, 20, 0, 20) }; DockPanel.SetDock(lines, Dock.Top); root.Children.Add(lines);
        for (int i = 0; i < 3; i++) { var line = new Border { Height = 2, Margin = new Thickness(0, 0, 8, 0) }; line.SetResourceReference(Border.BackgroundProperty, i <= step ? "AccentBrush" : "LineBrush"); lines.Children.Add(line); }
        var actions = new DockPanel { Margin = new Thickness(0, 16, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var next = Ui.Button(step == 2 ? "Finish" : "Next", () => { if (step < 2) { step++; Build(); } else { if (applyKeys != null && !applyKeys()) return; vm.Settings.OnboardingComplete = true; vm.Save(); Close(); } }, true); DockPanel.SetDock(next, Dock.Right); actions.Children.Add(next);
        if (step > 0) { var previous = Ui.Button("Previous", () => { step--; Build(); }); previous.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(previous); }
        var status = Ui.Text("", "MutedBrush"); status.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Status") { Source = vm }); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var body = new StackPanel(); root.Children.Add(body);
        body.Children.Add(Ui.Text(new[] { "StepMonitor", "StepPreset", "StepHotkey" }[step], "TextBrush", true)); body.Children.Add(Ui.Text("OnboardingHelp", "MutedBrush")); body.Children.Add(Ui.Line());
        if (step == 0) editor.AddMonitorChoice(body);
        else if (step == 1)
        {
            var choice = new ComboBox { ItemsSource = vm.Presets, DisplayMemberPath = "Name", SelectedItem = vm.Presets.FirstOrDefault(p => p.Name == vm.Selected.Name) ?? vm.Presets[0] };
            choice.SelectionChanged += (_, _) => { if (choice.SelectedItem is Models.Profile p) vm.ApplyPreset(p); }; body.Children.Add(choice);
        }
        else { var view = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 225 }; var keys = new StackPanel(); view.Content = keys; body.Children.Add(view); applyKeys = editor.BuildHotkeys(keys); }
        Appearance.Reveal(body);
    }
}




