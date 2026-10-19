using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CrosshairApp.Services;

namespace CrosshairApp.Controls;

public sealed class ScrubField : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ScrubField), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ScrubField)d).Refresh()));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get; set; } = 0;
    public double Maximum { get; set; } = 100;
    public double Step { get; set; } = 1;
    private double displayScale = 1;
    public double DisplayScale { get => displayScale; set { displayScale = value; Refresh(); } }
    private string unit = "px";
    public string Unit { get => unit; set { unit = value; Refresh(); } }
    public event Action? InteractionStarted, InteractionCompleted;
    private readonly TextBlock readout = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBox input = new() { Visibility = Visibility.Collapsed, HorizontalContentAlignment = HorizontalAlignment.Right };
    private readonly Border face = new() { Padding = new Thickness(6, 0, 6, 0), Background = Brushes.Transparent };
    private Point origin;
    private double start;
    private bool dragging, active, releasing;
    public ScrubField()
    {
        Focusable = true; MinWidth = 72; Height = 32;
        var grid = new Grid(); face.Child = readout; grid.Children.Add(face); grid.Children.Add(input); Content = grid;
        readout.SetResourceReference(TextBlock.FontFamilyProperty, "NumberFont"); readout.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        input.SetResourceReference(TextBox.FontFamilyProperty, "NumberFont");
        ToolTip = Appearance.Text("ScrubHelp");
        face.MouseLeftButtonDown += (_, e) => { Focus(); start = Value; origin = e.GetPosition(this); dragging = false; Begin(); face.CaptureMouse(); e.Handled = true; };
        face.MouseMove += (_, e) =>
        {
            if (!face.IsMouseCaptured) return;
            double delta = e.GetPosition(this).X - origin.X;
            if (Math.Abs(delta) > 3) dragging = true;
            if (dragging) SetCurrentValue(ValueProperty, DragValue(start, delta, Step, Keyboard.Modifiers, Minimum, Maximum));
        };
        face.MouseLeftButtonUp += (_, e) =>
        {
            if (!face.IsMouseCaptured) return;
            releasing = true; face.ReleaseMouseCapture(); releasing = false;
            if (dragging) End(); else Edit(); e.Handled = true;
        };
        face.LostMouseCapture += (_, _) => { if (!releasing) End(); };
        input.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } else if (e.Key == Key.Escape) { SetCurrentValue(ValueProperty, start); CloseInput(); e.Handled = true; } };
        input.LostKeyboardFocus += (_, _) => { if (input.Visibility == Visibility.Visible) Commit(); };
        PreviewKeyDown += (_, e) =>
        {
            if (input.Visibility == Visibility.Visible) return;
            if (e.Key is Key.Enter or Key.F2) { start = Value; Begin(); Edit(); e.Handled = true; }
            else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            { Begin(); SetCurrentValue(ValueProperty, DragValue(Value, e.Key is Key.Left or Key.Down ? -1 : 1, Step, Keyboard.Modifiers, Minimum, Maximum)); End(); e.Handled = true; }
        };
        GotKeyboardFocus += (_, _) => { face.BorderThickness = (Thickness)FindResource("FocusThickness"); face.SetResourceReference(Border.BorderBrushProperty, "FocusBrush"); };
        LostKeyboardFocus += (_, _) => { if (!IsKeyboardFocusWithin) face.BorderThickness = new Thickness(0); };
        Refresh();
    }
    public static double DragValue(double start, double delta, double step, ModifierKeys modifiers, double min, double max)
    {
        double multiplier = modifiers.HasFlag(ModifierKeys.Shift) ? .1 : modifiers.HasFlag(ModifierKeys.Control) ? 10 : 1;
        return Math.Clamp(Math.Round(start + delta * step * multiplier, 3), min, max);
    }
    private void Begin() { if (active) return; active = true; InteractionStarted?.Invoke(); }
    private void End() { if (!active) return; active = false; InteractionCompleted?.Invoke(); }
    private void Edit() { input.Visibility = Visibility.Visible; face.Visibility = Visibility.Hidden; input.Text = (Value * DisplayScale).ToString("0.###", CultureInfo.InvariantCulture); input.Focus(); input.SelectAll(); }
    private void Commit()
    {
        if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number / DisplayScale >= Minimum && number / DisplayScale <= Maximum)
        { SetCurrentValue(ValueProperty, number / DisplayScale); CloseInput(); }
        else { input.ToolTip = Appearance.Text("NumberInvalid"); input.SetResourceReference(TextBox.BorderBrushProperty, "AccentBrush"); if (!input.IsKeyboardFocused) { SetCurrentValue(ValueProperty, start); CloseInput(); } }
    }
    private void CloseInput() { input.Visibility = Visibility.Collapsed; face.Visibility = Visibility.Visible; End(); Refresh(); }
    private void Refresh() { readout.Text = (Value * DisplayScale).ToString("0.##", CultureInfo.InvariantCulture) + " " + Unit; face.ToolTip = readout.Text + "\n" + Appearance.Text("ScrubHelp"); }
}

