using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;

namespace CrosshairApp.Controls;

public sealed class ParameterRow : StackPanel
{
    public ParameterRow(EditorViewModel vm, string property, string label, double min, double max, double step, double defaultValue, string unit = "px", double displayScale = 1)
    {
        Margin = new Thickness(0, 0, 0, 5);
        var header = new DockPanel(); Children.Add(header);
        var reset = Ui.IconButton("Reset", "Reset", () => { vm.History.Begin(); typeof(Models.Profile).GetProperty(property)!.SetValue(vm.Selected, defaultValue); vm.History.End(); });
        reset.Width = 24; reset.Height = 24; reset.MinHeight = 24; reset.Margin = new Thickness(4, 0, 0, 0); reset.Padding = new Thickness(4);
        reset.SetBinding(VisibilityProperty, new Binding("Selected." + property) { Converter = new ChangedFromDefault(defaultValue) }); DockPanel.SetDock(reset, Dock.Right); header.Children.Add(reset);
        var number = new ScrubField { Minimum = min, Maximum = max, Step = step, DisplayScale = displayScale, Unit = unit };
        number.SetBinding(ScrubField.ValueProperty, new Binding("Selected." + property) { Mode = BindingMode.TwoWay }); number.InteractionStarted += vm.History.Begin; number.InteractionCompleted += vm.History.End;
        System.Windows.Automation.AutomationProperties.SetName(number, Appearance.Text(label)); DockPanel.SetDock(number, Dock.Right); header.Children.Add(number);
        header.Children.Add(new TrackedLabel(label) { VerticalAlignment = VerticalAlignment.Center });
        var track = new Grid(); Children.Add(track);
        var slider = new Slider { Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 10, TickFrequency = step, IsSnapToTickEnabled = false };
        slider.SetBinding(RangeBase.ValueProperty, new Binding("Selected." + property) { Mode = BindingMode.TwoWay });
        slider.SetBinding(ToolTipProperty, new Binding("Selected." + property) { StringFormat = "{0:0.##} " + (displayScale == 100 ? "×100 %" : unit) });
        System.Windows.Automation.AutomationProperties.SetName(slider, Appearance.Text(label));
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => vm.History.Begin()), true);
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => vm.History.End()), true);
        track.Children.Add(new DefaultTick((defaultValue - min) / (max - min))); track.Children.Add(slider);
    }
    private sealed class ChangedFromDefault(double original) : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is double d && Math.Abs(d - original) > .0001 ? Visibility.Visible : Visibility.Hidden;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
    private sealed class DefaultTick(double fraction) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            double x = 5 + Math.Clamp(fraction, 0, 1) * Math.Max(0, ActualWidth - 10);
            dc.DrawLine(new Pen((Brush)FindResource("SecondaryBrush"), 1), new Point(x, 6), new Point(x, 18));
        }
    }
}

