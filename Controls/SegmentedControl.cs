using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CrosshairApp.Controls;

public sealed class SegmentedControl : UserControl
{
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(string), typeof(SegmentedControl), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SegmentedControl)d).Update()));
    public string Selected { get => (string)GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    private readonly List<(string key, Button button)> buttons = new();
    public event Action<string>? SelectionChanged;
    public SegmentedControl(params (string key, string label, string? icon)[] choices)
    {
        if (choices.Length > 5) throw new ArgumentException("Segment groups contain at most five choices.");
        var panel = new UniformGrid { Columns = choices.Length }; Content = panel;
        foreach (var c in choices)
        {
            var b = new Button { Content = c.icon == null ? Services.Appearance.Text(c.label) : Ui.Icon(c.icon), ToolTip = Services.Appearance.Text(c.label), Margin = new Thickness(0, 0, 2, 0), Padding = new Thickness(4), MinWidth = 26 };
            System.Windows.Automation.AutomationProperties.SetName(b, Services.Appearance.Text(c.label));
            b.Click += (_, _) => Choose(c.key); buttons.Add((c.key, b)); panel.Children.Add(b);
        }
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Left or Key.Right)) return;
            int i = Math.Max(0, buttons.FindIndex(b => b.key == Selected)); i = (i + (e.Key == Key.Left ? -1 : 1) + buttons.Count) % buttons.Count;
            Choose(buttons[i].key); buttons[i].button.Focus(); e.Handled = true;
        };
    }
    private void Choose(string key) { SetCurrentValue(SelectedProperty, key); SelectionChanged?.Invoke(key); }
    private void Update()
    {
        foreach (var (key, b) in buttons) { b.SetResourceReference(BorderBrushProperty, key == Selected ? "AccentBrush" : "LineBrush"); b.BorderThickness = key == Selected ? new Thickness(1, 1, 1, 2) : new Thickness(1); }
    }
}
