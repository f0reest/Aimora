using System.Windows;
using System.Windows.Controls;
using CrosshairApp.Services;

namespace CrosshairApp.Controls;
public sealed class SectionHeader : StackPanel
{
    public StackPanel Body { get; } = new() { Margin = new Thickness(0, 4, 0, 12) };
    public bool Expanded { get => Body.Visibility == Visibility.Visible; set { Body.Visibility = value ? Visibility.Visible : Visibility.Collapsed; if (value) Appearance.Reveal(Body); } }
    public SectionHeader(string key, bool expanded = true)
    {
        var header = new DockPanel(); var icon = Ui.Icon("Chevron"); DockPanel.SetDock(icon, Dock.Right); header.Children.Add(icon); header.Children.Add(new TrackedLabel(key));
        var button = new Button { Content = header, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => Expanded = !Expanded; Children.Add(button); Children.Add(Body); Expanded = expanded;
        System.Windows.Automation.AutomationProperties.SetName(button, Appearance.Text(key));
    }
}
