using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using CrosshairApp.Controls;
using CrosshairApp.Models;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CrosshairApp.Views;

public partial class EditorWindow : Window
{
    private readonly EditorViewModel vm;
    private readonly OverlayWindow overlay;
    private StageView stage = null!;
    private StackPanel strip = null!;
    private Button undo = null!, redo = null!;
    private readonly List<CrosshairVisual> thumbnails = new();
    private readonly List<(string key, Border marker)> navigation = new();
    private string page = "Editor", filter = "All";
    private string backgroundKind;
    private double zoom = 4;
    private BitmapSource? customBackground;
    private bool comparing;
    private Profile? compared;
    private SegmentedControl? shapeA, shapeB;
    private TextBlock compareLabel = null!;
    private MiniHudWindow? hud;
    public bool Exiting { get; set; }
    public EditorViewModel ViewModel => vm;
    public StageView Stage => stage;
    public string CurrentPage => page;

    public EditorWindow(EditorViewModel model, OverlayWindow overlay)
    {
        vm = model; this.overlay = overlay; backgroundKind = vm.Settings.TestBackground;
        InitializeComponent(); DataContext = vm; Title = Appearance.Text("AppName");
        BuildShell();
        vm.SelectionChanged += SelectionChanged;
        vm.Profiles.CollectionChanged += ProfilesChanged;
        vm.History.Changed += HistoryChanged;
        InputBindings.Add(new KeyBinding(new RelayCommand(vm.Save), Key.S, ModifierKeys.Control));
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Z or Key.Y)
            {
                Keyboard.ClearFocus(); if (e.Key == Key.Z) vm.History.Undo(); else vm.History.Redo(); e.Handled = true;
            }
        };
        Deactivated += (_, _) => EndCompare();
        StateChanged += (_, _) => { ShellRoot.Margin = WindowState == WindowState.Maximized ? new Thickness(6) : new Thickness(0); if (WindowState == WindowState.Minimized && vm.Settings.MinimizeToTray) Hide(); };
        Closing += (_, e) => { EndCompare(); if (!Exiting) { e.Cancel = true; Hide(); vm.ScheduleSave(); } };
        Closed += (_, _) => { vm.SelectionChanged -= SelectionChanged; vm.Profiles.CollectionChanged -= ProfilesChanged; vm.History.Changed -= HistoryChanged; stage.Dispose(); DetachThumbnails(); hud?.Close(); };
        Loaded += (_, _) => { if (!vm.Settings.OnboardingComplete) ShowOnboarding(); };
    }
    private void BuildShell()
    {
        EndCompare(); BuildTitle(); BuildRail(); BuildStage(); BuildInspector(); HistoryChanged();
    }
    private void BuildTitle()
    {
        TitleHost.Children.Clear();
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(controls, Dock.Right); TitleHost.Children.Add(controls);
        var save = Ui.Button("Save", () => { Keyboard.ClearFocus(); vm.Save(); }, true); save.Margin = new Thickness(0, 0, 16, 0); controls.Children.Add(save);
        controls.Children.Add(Ui.IconButton("Minimize", "Minimize", () => WindowState = WindowState.Minimized));
        controls.Children.Add(Ui.IconButton("Maximize", "Maximize", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
        controls.Children.Add(Ui.IconButton("Close", "Close", Close)); WindowChrome.SetIsHitTestVisibleInChrome(controls, true);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = Ui.Icon("Editor"); icon.Margin = new Thickness(0, 0, 12, 0); icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush"); brand.Children.Add(icon);
        brand.Children.Add(Ui.Text("AppName", "TextBrush", true)); TitleHost.Children.Add(brand);
    }
    private void BuildRail()
    {
        RailHost.Children.Clear(); navigation.Clear();
        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 12) }; DockPanel.SetDock(bottom, Dock.Bottom); RailHost.Children.Add(bottom);
        var hudButton = Ui.IconButton("Hud", "MiniHud", ShowHud); hudButton.Width = 40; hudButton.Height = 40; bottom.Children.Add(hudButton);
        var rail = new StackPanel { Margin = new Thickness(0, 16, 0, 0) }; RailHost.Children.Add(rail);
        foreach (var key in new[] { "Editor", "Presets", "Profiles", "Hotkeys", "Settings" })
        {
            var b = Ui.IconButton(key, key, () => Navigate(key)); b.Width = 60; b.Height = 48; b.BorderThickness = new Thickness(0); b.HorizontalAlignment = HorizontalAlignment.Stretch;
            var marker = new Border { BorderThickness = new Thickness(2, 0, 0, 0), Child = b, Margin = new Thickness(0, 0, 0, 8) };
            navigation.Add((key, marker)); rail.Children.Add(marker);
        }
    }
    private void BuildStage()
    {
        stage?.Dispose(); DetachThumbnails(); StageHost.Children.Clear(); StageHost.RowDefinitions.Clear();
        foreach (var height in new[] { (GridLength)FindResource("StageToolbarRow"), new GridLength(1, GridUnitType.Star), (GridLength)FindResource("BackgroundToolbarRow"), (GridLength)FindResource("PresetStripRow") }) StageHost.RowDefinitions.Add(new RowDefinition { Height = height });
        var header = new DockPanel { Margin = new Thickness(0, 12, 0, 8) }; StageHost.Children.Add(header);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(tools, Dock.Right); header.Children.Add(tools);
        undo = Ui.IconButton("Undo", "Undo", vm.History.Undo); redo = Ui.IconButton("Redo", "Redo", vm.History.Redo); tools.Children.Add(undo); tools.Children.Add(redo);
        var compare = Ui.IconButton("Compare", "Compare", () => { }); tools.Children.Add(compare);
        compare.PreviewMouseLeftButtonDown += (_, e) => { BeginCompare(); compare.CaptureMouse(); e.Handled = true; };
        compare.PreviewMouseLeftButtonUp += (_, e) => { EndCompare(); compare.ReleaseMouseCapture(); e.Handled = true; };
        compare.LostMouseCapture += (_, _) => EndCompare();
        compare.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space) { BeginCompare(); e.Handled = true; } };
        compare.PreviewKeyUp += (_, e) => { if (e.Key == Key.Space) { EndCompare(); e.Handled = true; } };
        compare.LostKeyboardFocus += (_, _) => EndCompare();
        var toggle = Ui.IconButton("Eye", "Toggle", overlay.Toggle); tools.Children.Add(toggle);
        var name = new StackPanel(); name.Children.Add(new TrackedLabel("Stage"));
        var title = Ui.Text("", "TextBrush", true); title.SetBinding(TextBlock.TextProperty, new Binding("Selected.Name")); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap; name.Children.Add(title); header.Children.Add(name);
        stage = new StageView { Zoom = zoom }; Grid.SetRow(stage, 1); StageHost.Children.Add(stage); stage.SetBackground(backgroundKind, customBackground); stage.Apply(vm.Selected);
        stage.AllowDrop = true;
        stage.DragOver += (sender, e) =>
        {
            e.Effects = TryGetDroppedPng(e.Data, out string ignored) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        stage.Drop += (_, e) => { if (TryGetDroppedPng(e.Data, out string path)) Guard(() => ImportAssetFile(path)); e.Handled = true; };
        var zoomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 10) };
        var zoomControl = new SegmentedControl(("1", "1×", null), ("4", "4×", null), ("8", "8×", null)) { Width = 132, Selected = zoom.ToString(CultureInfo.InvariantCulture) };
        zoomControl.SetResourceReference(BackgroundProperty, "CanvasBrush"); zoomControl.SelectionChanged += value => { zoom = double.Parse(value, CultureInfo.InvariantCulture); stage.Zoom = zoom; }; zoomPanel.Children.Add(zoomControl); stage.Children.Add(zoomPanel);
        compareLabel = Ui.Text("Comparing", "AccentBrush"); compareLabel.Visibility = Visibility.Collapsed; compareLabel.HorizontalAlignment = HorizontalAlignment.Left; compareLabel.VerticalAlignment = VerticalAlignment.Bottom; compareLabel.Margin = new Thickness(36, 0, 0, 16); stage.Children.Add(compareLabel);
        var backgrounds = new Grid { Margin = new Thickness(0, 8, 0, 8) }; Grid.SetRow(backgrounds, 2); StageHost.Children.Add(backgrounds);
        backgrounds.ColumnDefinitions.Add(new()); backgrounds.ColumnDefinitions.Add(new() { Width = new GridLength(84) });
        var bg = new SegmentedControl(("Grass", "Grass", null), ("Concrete", "Concrete", null), ("Sky", "Sky", null), ("Snow", "Snow", null), ("Dark", "Dark", null)) { Selected = backgroundKind };
        bg.SelectionChanged += kind => SetBackground(kind); backgrounds.Children.Add(bg);
        var custom = Ui.Button("Custom", () => Guard(() => { var d = new OpenFileDialog { Filter = Appearance.Text("ImageFilter") + "|*.png;*.jpg;*.jpeg;*.bmp" }; if (d.ShowDialog(this) == true) { customBackground = TestTextures.Load(d.FileName); SetBackground("Custom"); bg.Selected = ""; } })); custom.Margin = new Thickness(4, 0, 0, 0); Grid.SetColumn(custom, 1); backgrounds.Children.Add(custom);
        var footer = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; Grid.SetRow(footer, 3); StageHost.Children.Add(footer);
        var label = new TrackedLabel("Presets") { Margin = new Thickness(0, 3, 0, 6) }; DockPanel.SetDock(label, Dock.Top); footer.Children.Add(label);
        strip = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        var scroller = new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false };
        scroller.PreviewMouseWheel += (_, e) => { scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta); e.Handled = true; }; footer.Children.Add(scroller); BuildStrip();
    }
    public void SetBackground(string kind) { backgroundKind = kind; vm.Settings.TestBackground = kind == "Custom" ? "Concrete" : kind; stage.SetBackground(kind, kind == "Custom" ? customBackground : null); vm.ScheduleSave(); BuildStrip(); }
    private void DetachThumbnails() { foreach (var t in thumbnails) t.Profile = null; thumbnails.Clear(); }
    private void BuildStrip()
    {
        if (strip == null) return; DetachThumbnails(); strip.Children.Clear();
        var choices = new List<(Profile profile, bool mine)>();
        if (filter != "Mine") choices.AddRange(vm.Presets.Select(p => (p, false)));
        if (filter != "Builtin") choices.AddRange(vm.Profiles.Select(p => (p, true)));
        int i = 0;
        foreach (var (p, mine) in choices.OrderByDescending(c => c.mine ? ReferenceEquals(c.profile, vm.Selected) : !vm.Profiles.Contains(vm.Selected) && c.profile.Name == vm.Selected.Name))
        {
            bool active = mine ? ReferenceEquals(p, vm.Selected) : !vm.Profiles.Contains(vm.Selected) && p.Name == vm.Selected.Name;
            var body = new StackPanel(); var patch = new Grid { Height = active ? 58 : 48, ClipToBounds = true };
            patch.Children.Add(new Image { Source = customBackground != null && backgroundKind == "Custom" ? customBackground : TestTextures.Get(backgroundKind), Stretch = Stretch.UniformToFill });
            var visual = new CrosshairVisual { Profile = p, Zoom = 1 }; thumbnails.Add(visual); patch.Children.Add(visual); body.Children.Add(patch);
            body.Children.Add(new TrackedLabel("Mine") { Text = p.Name.ToUpperInvariant(), Margin = new Thickness(6, 5, 6, 0) });
            var type = Ui.Text(mine ? "Mine" : "Builtin", "MutedBrush"); type.SetResourceReference(TextBlock.FontSizeProperty, "LabelSize"); type.Margin = new Thickness(6, 0, 6, 4); body.Children.Add(type);
            var b = new Button { Content = body, Width = active ? 156 : 112 + (i++ % 3) * 8, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Bottom, HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = p.Name };
            b.SetResourceReference(BorderBrushProperty, active ? "AccentBrush" : "LineBrush");
            b.Click += (_, _) => { EndCompare(); if (mine) vm.Selected = p; else vm.ApplyPreset(p); }; strip.Children.Add(b);
        }
        if (choices.Count == 0) strip.Children.Add(Ui.Text("EmptyProfiles", "MutedBrush"));
    }
    public void Navigate(string key)
    {
        EndCompare(); Keyboard.ClearFocus(); page = key; BuildInspector(); Appearance.Reveal(InspectorBody);
    }
    private void BuildInspector()
    {
        shapeA = null; shapeB = null; InspectorTitle.Text = Appearance.Text(page).ToUpperInvariant(); InspectorBody.Children.Clear(); InspectorScroll.ScrollToTop();
        foreach (var (key, marker) in navigation) { if (key == page) marker.SetResourceReference(Border.BorderBrushProperty, "AccentBrush"); else marker.BorderBrush = Brushes.Transparent; }
        switch (page)
        {
            case "Editor": BuildEditor(); break;
            case "Presets": BuildPresets(); break;
            case "Profiles": BuildProfiles(); break;
            case "Hotkeys": BuildHotkeys(InspectorBody); break;
            case "Settings": BuildSettings(); break;
        }
    }
    private void BuildEditor()
    {
        var shape = new SectionHeader("Shape"); InspectorBody.Children.Add(shape);
        var first = new SegmentedControl(("Cross", "Cross", "Cross"), ("CrossDot", "CrossDot", "CrossDot"), ("Dot", "Dot", "Dot"), ("Circle", "Circle", "Circle"), ("TShape", "TShape", "TShape")) { Selected = vm.Selected.Style.ToString() };
        var second = new SegmentedControl(("XShape", "XShape", "XShape"), ("Chevron", "Chevron", "ShapeChevron"), ("Square", "Square", "Square"), ("Custom", "Custom", "Custom")) { Selected = vm.Selected.Style.ToString(), Margin = new Thickness(0, 4, 0, 8) };
        shapeA = first; shapeB = second;
        void StyleChanged(string value) { vm.Selected.Style = Enum.Parse<CrosshairStyle>(value); first.Selected = value; second.Selected = value; }
        first.SelectionChanged += StyleChanged; second.SelectionChanged += StyleChanged; shape.Body.Children.Add(first); shape.Body.Children.Add(second);
        AddButton(shape.Body, "ImportPng", ImportPng);
        shape.Body.Children.Add(Ui.Text("DropPng", "MutedBrush"));
        shape.Body.Children.Add(new ParameterRow(vm, "Thickness", "Thickness", .5, 20, .5, 2));
        shape.Body.Children.Add(new ParameterRow(vm, "Length", "Length", 0, 100, 1, 12));
        shape.Body.Children.Add(new ParameterRow(vm, "Gap", "Gap", 0, 100, 1, 4));
        shape.Body.Children.Add(new ParameterRow(vm, "DotSize", "DotSize", 0, 40, 1, 3));
        shape.Body.Children.Add(new ParameterRow(vm, "Rotation", "Rotation", -180, 180, 1, 0, "°"));
        var color = new SectionHeader("Color", false); InspectorBody.Children.Add(color); AddColor(color.Body, "Color"); color.Body.Children.Add(new ParameterRow(vm, "Opacity", "Opacity", 0, 1, .01, 1, "%", 100));
        var outline = new SectionHeader("Outline", false); InspectorBody.Children.Add(outline); AddToggle(outline.Body, "Enabled", "Selected.Outline"); AddColor(outline.Body, "OutlineColor"); outline.Body.Children.Add(new ParameterRow(vm, "OutlineThickness", "OutlineWidth", 0, 10, .5, 1));
        var behavior = new SectionHeader("Behavior", false); InspectorBody.Children.Add(behavior); AddToggle(behavior.Body, "Dynamic", "Selected.DynamicColor"); AddToggle(behavior.Body, "Pulse", "Selected.Pulse");
        behavior.Body.Children.Add(new ParameterRow(vm, "OffsetX", "OffsetX", -8000, 8000, 1, 0)); behavior.Body.Children.Add(new ParameterRow(vm, "OffsetY", "OffsetY", -8000, 8000, 1, 0));
        AddButton(behavior.Body, "ImportImage", ImportImage); behavior.Body.Children.Add(new ParameterRow(vm, "ImageSize", "ImageSize", 4, 256, 1, 48)); behavior.Body.Children.Add(Ui.Text("AssetsHelp", "MutedBrush"));
    }
    private void AddColor(Panel panel, string property)
    {
        var picker = new Controls.ColorPicker(); picker.SetBinding(Controls.ColorPicker.ColorProperty, new Binding("Selected." + property) { Mode = BindingMode.TwoWay });
        picker.InteractionStarted += vm.History.Begin; picker.InteractionCompleted += vm.History.End;
        picker.SetRecent(vm.Settings.RecentColors);
        picker.ColorCommitted += c => { vm.Settings.RecentColors.Remove(c); vm.Settings.RecentColors.Insert(0, c); vm.Settings.RecentColors = vm.Settings.RecentColors.Take(8).ToList(); picker.SetRecent(vm.Settings.RecentColors); vm.ScheduleSave(); }; panel.Children.Add(picker);
    }
    private void BuildPresets()
    {
        InspectorBody.Children.Add(Ui.Text("PresetHelp", "MutedBrush"));
        var source = new SegmentedControl(("All", "All", null), ("Builtin", "Builtin", null), ("Mine", "Mine", null)) { Selected = filter, Margin = new Thickness(0, 16, 0, 16) }; source.SelectionChanged += value => { filter = value; BuildStrip(); }; InspectorBody.Children.Add(source);
        InspectorBody.Children.Add(new TrackedLabel("Working")); var name = Ui.Text("", "TextBrush", true); name.SetBinding(TextBlock.TextProperty, new Binding("Selected.Name")); InspectorBody.Children.Add(name);
        AddButton(InspectorBody, "Editor", () => Navigate("Editor"));
    }
    private void BuildProfiles()
    {
        InspectorBody.Children.Add(Ui.Text("ProfileHelp", "MutedBrush"));
        if (vm.Profiles.Count == 0) InspectorBody.Children.Add(Ui.Text("EmptyProfiles", "MutedBrush"));
        else foreach (var p in vm.Profiles)
        {
            var b = new Button { Content = p.Name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            b.Click += (_, _) => vm.Selected = p; if (p == vm.Selected) b.SetResourceReference(BorderBrushProperty, "AccentBrush"); InspectorBody.Children.Add(b);
        }
        InspectorBody.Children.Add(Ui.Line()); AddText(InspectorBody, "Rename", "Selected.Name");
        var actions = new UniformGrid { Columns = 2 }; InspectorBody.Children.Add(actions);
        AddButton(actions, "Duplicate", () => vm.DuplicateCommand.Execute(null)); AddButton(actions, "Delete", () => vm.DeleteCommand.Execute(null));
        AddButton(actions, "Import", ImportProfile); AddButton(actions, "Export", ExportProfile);
        AddButton(actions, "CopyCode", () => { Clipboard.SetText(Storage.Share(vm.Selected)); vm.Status = Appearance.Text("Copied"); }); AddButton(actions, "PasteCode", () => vm.Add(Storage.Import(Clipboard.GetText())));
    }
    public Func<bool> BuildHotkeys(Panel host)
    {
        host.Children.Add(Ui.Text("HotkeyHelp", "MutedBrush"));
        var toggle = KeyChoice(host, "ToggleKey", vm.Settings.ToggleKey); var previous = KeyChoice(host, "PreviousKey", vm.Settings.PreviousKey); var next = KeyChoice(host, "NextKey", vm.Settings.NextKey);
        bool Apply() => ApplyKeys((int)toggle.SelectedValue, (int)previous.SelectedValue, (int)next.SelectedValue);
        AddButton(host, "Apply", () => Apply()); return Apply;
    }
    public bool ApplyKeys(int toggle, int previous, int next)
    {
        if (new[] { toggle, previous, next }.Distinct().Count() != 3) { vm.Status = Appearance.Text("HotkeyConflict"); return false; }
        var app = (App)Application.Current;
        if (!app.ApplyHotkeys(toggle, previous, next)) { vm.Status = Appearance.Text("HotkeyConflict"); return false; }
        vm.Settings.ToggleKey = toggle; vm.Settings.PreviousKey = previous; vm.Settings.NextKey = next; vm.NotifyPreferences(); vm.Status = Appearance.Text("HotkeysSaved"); return true;
    }
    private static ComboBox KeyChoice(Panel panel, string key, int selected)
    {
        panel.Children.Add(new TrackedLabel(key) { Margin = new Thickness(0, 16, 0, 4) });
        var c = new ComboBox { ItemsSource = Enumerable.Range(1, 12).Select(n => new { Value = n, Label = "Ctrl + Alt + F" + n }), DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = selected }; panel.Children.Add(c); return c;
    }
    private void BuildSettings()
    {
        InspectorBody.Children.Add(new TrackedLabel("Theme"));
        var theme = new SegmentedControl(("Optic", "Optic", null), ("Paper", "Paper", null), ("Signal", "Signal", null)) { Selected = vm.Settings.Theme, Margin = new Thickness(0, 4, 0, 16) };
        theme.SelectionChanged += value => { vm.Settings.Theme = value; RefreshAppearance(); }; InspectorBody.Children.Add(theme);
        InspectorBody.Children.Add(new TrackedLabel("Language"));
        var language = new SegmentedControl(("id-ID", "Indonesia", null), ("en-US", "English", null)) { Selected = vm.Settings.Language, Margin = new Thickness(0, 4, 0, 16) };
        language.SelectionChanged += value => { vm.Settings.Language = value; RefreshAppearance(); }; InspectorBody.Children.Add(language);
        var motion = new ToggleSquare { Content = Appearance.Text("ReduceMotion"), IsChecked = vm.Settings.ReduceMotion }; motion.Click += (_, _) => { vm.Settings.ReduceMotion = motion.IsChecked == true; Appearance.ReduceMotion = vm.Settings.ReduceMotion; stage.Apply(vm.Selected); overlay.Apply(vm.Selected); vm.ScheduleSave(); }; InspectorBody.Children.Add(motion);
        InspectorBody.Children.Add(Ui.Line()); AddMonitorChoice(InspectorBody);
        var tray = new ToggleSquare { Content = Appearance.Text("MinimizeTray"), IsChecked = vm.Settings.MinimizeToTray, Margin = new Thickness(0, 12, 0, 0) }; tray.Click += (_, _) => { vm.Settings.MinimizeToTray = tray.IsChecked == true; vm.ScheduleSave(); }; InspectorBody.Children.Add(tray);
        var start = new ToggleSquare { Content = Appearance.Text("Startup"), IsChecked = StartupEnabled() }; InspectorBody.Children.Add(start);
        start.Click += (_, _) => Guard(() => { using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); if (start.IsChecked == true) { var exe = Environment.ProcessPath!; if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(Appearance.Text("StartupExe")); k.DeleteValue("CrosshairApp", false); k.SetValue("AIMORA", "\"" + exe + "\" --tray"); } else { k.DeleteValue("AIMORA", false); k.DeleteValue("CrosshairApp", false); } });
        AddButton(InspectorBody, "OpenOnboarding", ShowOnboarding); AddButton(InspectorBody, "MiniHud", ShowHud);
        var compatibility = new SectionHeader("Compatibility", false); InspectorBody.Children.Add(compatibility); compatibility.Body.Children.Add(Ui.Text("CompatibilityHelp", "MutedBrush")); compatibility.Body.Children.Add(new ToggleSquare { Content = Ui.Text("Experimental"), IsEnabled = false }); compatibility.Body.Children.Add(Ui.Text("AntiCheat", "MutedBrush"));
    }
    public void AddMonitorChoice(Panel host)
    {
        host.Children.Add(new TrackedLabel("Monitor")); var monitor = new ComboBox { Margin = new Thickness(0, 4, 0, 8) }; host.Children.Add(monitor);
        void Refresh() { monitor.ItemsSource = Forms.Screen.AllScreens.Select(s => s.DeviceName).ToArray(); monitor.SelectedItem = vm.Settings.Monitor; if (monitor.SelectedIndex < 0) monitor.SelectedItem = Forms.Screen.PrimaryScreen?.DeviceName; }
        monitor.SelectionChanged += (_, _) => { if (monitor.SelectedItem is string d) { vm.Settings.Monitor = d; overlay.MonitorName = d; overlay.Reposition(); vm.ScheduleSave(); } }; Refresh(); AddButton(host, "Refresh", Refresh);
    }
    public void RefreshAppearance()
    {
        Appearance.ReduceMotion = vm.Settings.ReduceMotion; Appearance.Apply(vm.Settings.Theme, vm.Settings.Language); vm.ScheduleSave(); BuildShell();
    }
    private static bool StartupEnabled() { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return k?.GetValue("AIMORA") != null || k?.GetValue("CrosshairApp") != null; }
    private void AddText(Panel p, string key, string path)
    {
        p.Children.Add(new TrackedLabel(key)); var t = new TextBox { Margin = new Thickness(0, 4, 0, 8), MaxLength = 80 };
        t.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        t.GotKeyboardFocus += (_, _) => vm.History.Begin(); t.LostKeyboardFocus += (_, _) => vm.History.End(); p.Children.Add(t);
    }
    private static void AddToggle(Panel p, string key, string path) { var t = new ToggleSquare { Content = Appearance.Text(key), Margin = new Thickness(0, 0, 0, 8) }; t.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay }); p.Children.Add(t); }
    private void AddButton(Panel p, string key, Action action) { var b = Ui.Button(key, () => Guard(action)); b.Margin = new Thickness(0, 8, 4, 0); p.Children.Add(b); }
    private void Guard(Action action) { try { action(); } catch (Exception ex) { vm.Status = ex.Message; } }
    private void ImportPng()
    {
        var d = new OpenFileDialog { Filter = Appearance.Text("PngFilter") + "|*.png", Multiselect = false };
        if (d.ShowDialog(this) == true) ImportAssetFile(d.FileName);
    }
    private void ImportImage()
    {
        var d = new OpenFileDialog { Filter = Appearance.Text("ImageFilter") + "|*.png;*.svg", Multiselect = false };
        if (d.ShowDialog(this) == true) ImportAssetFile(d.FileName);
    }
    private static bool TryGetDroppedPng(IDataObject data, out string path)
    {
        path = "";
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] files || files.Length != 1) return false;
        path = files[0]; return Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);
    }
    private void ImportAssetFile(string path)
    {
        string type = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (type is not ("png" or "svg")) throw new FormatException("PNG / SVG");
        if (new FileInfo(path).Length > 2_000_000) throw new FormatException(Appearance.Text("AssetTooLarge"));
        var p = vm.Selected.Clone(); p.AssetType = type; p.Asset = Convert.ToBase64String(File.ReadAllBytes(path)); AssetLoader.Load(p);
        vm.History.Begin(); vm.Selected.AssetType = p.AssetType; vm.Selected.Asset = p.Asset; vm.Selected.Style = CrosshairStyle.Custom; vm.History.End(); BuildInspector();
        vm.Status = Appearance.Text(type == "png" ? "PngImported" : "AssetImported") + Path.GetFileName(path);
    }
    private void ImportProfile() { var d = new OpenFileDialog { Filter = Appearance.Text("JsonFilter") + "|*.json" }; if (d.ShowDialog(this) == true) { if (new FileInfo(d.FileName).Length > 4_000_000) throw new FormatException(Appearance.Text("JsonTooLarge")); vm.Add(Storage.Import(File.ReadAllText(d.FileName))); } }
    private void ExportProfile() { var d = new SaveFileDialog { Filter = Appearance.Text("JsonFilter") + "|*.json", FileName = "crosshair.json" }; if (d.ShowDialog(this) == true) File.WriteAllText(d.FileName, Storage.Export(vm.Selected)); }
    private void SelectionChanged(Profile p) { EndCompare(); stage.Apply(p, true); BuildStrip(); BuildInspector(); HistoryChanged(); }
    private void ProfilesChanged(object? s, NotifyCollectionChangedEventArgs e) { BuildStrip(); if (page == "Profiles") BuildInspector(); }
    private void HistoryChanged() { if (shapeA != null) shapeA.Selected = vm.Selected.Style.ToString(); if (shapeB != null) shapeB.Selected = vm.Selected.Style.ToString(); if (undo == null) return; undo.IsEnabled = vm.History.CanUndo; redo.IsEnabled = vm.History.CanRedo; }
    public void BeginCompare() { if (comparing) return; comparing = true; compared = vm.History.Baseline; stage.Apply(compared); overlay.Apply(compared); compareLabel.Visibility = Visibility.Visible; }
    public void EndCompare() { if (!comparing) return; comparing = false; stage.Apply(vm.Selected); overlay.Apply(vm.Selected); compareLabel.Visibility = Visibility.Collapsed; compared = null; }
    public void ShowHud() { if (hud == null || !hud.IsLoaded) { hud = new MiniHudWindow(vm); hud.Closed += (_, _) => hud = null; hud.Show(); } else hud.Activate(); }
    public void ShowOnboarding() { var onboarding = new OnboardingWindow(this, vm) { Owner = this }; onboarding.ShowDialog(); }
    public void OpenEditor()
    {
        // Restore state before showing. Showing a hidden minimized HWND first can
        // immediately send it back through the minimize-to-tray handler.
        PrepareTrayRestore();
        if (!IsVisible) Show();
        Activate(); Focus();
    }
    internal void PrepareTrayRestore()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
    }
}




