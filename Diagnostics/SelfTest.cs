using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairApp.Controls;
using CrosshairApp.Models;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
using CrosshairApp.Views;
namespace CrosshairApp.Diagnostics;
internal static class SelfTest
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T found) yield return found;
        foreach (var child in LogicalTreeHelper.GetChildren(parent)) if (child is DependencyObject d) foreach (var match in Find<T>(d)) yield return match;
    }
    public static void Snapshot(FrameworkElement content, string name, int width, int height)
    {
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create("artifacts/" + name + ".png"); encoder.Save(file);
    }
    public static int Run()
    {
        Directory.CreateDirectory("artifacts"); var log = new List<string>(); EditorWindow? editor = null; OverlayWindow? overlay = null;
        try
        {
            Appearance.Apply("Optic", "id-ID"); Appearance.ReduceMotion = true;
            var presets = Storage.Presets(); Assert(presets.Count >= 24, "Preset count");
            foreach (var p in presets) Assert(Storage.Export(p) == Storage.Export(Storage.Import(Storage.Share(p))), "Profile round-trip");
            var whiteProfile = presets.First(p => p.Name == "White Micro Cross").Clone(); whiteProfile.Outline = false;
            var whiteVisual = new CrosshairVisual { Profile = whiteProfile, Width = 44, Height = 44 };
            whiteVisual.Measure(new Size(44, 44)); whiteVisual.Arrange(new Rect(0, 0, 44, 44)); whiteVisual.UpdateLayout();
            var whiteBitmap = new RenderTargetBitmap(44, 44, 96, 96, PixelFormats.Pbgra32); whiteBitmap.Render(whiteVisual);
            var whitePixels = new byte[44 * 44 * 4]; whiteBitmap.CopyPixels(whitePixels, 44 * 4, 0);
            Assert(Enumerable.Range(0, whitePixels.Length / 4).Any(i => whitePixels[i * 4] >= 250 && whitePixels[i * 4 + 1] >= 250 && whitePixels[i * 4 + 2] >= 250 && whitePixels[i * 4 + 3] >= 250), "White 1 px stroke was dimmed by antialiasing");
            log.Add($"PASS: {presets.Count} presets validated, JSON/share-code round-trip, crisp white 1 px rendering");
            var sourcePixels = new byte[16 * 16 * 4];
            for (int y = 5; y < 11; y++) for (int x = 6; x < 10; x++) { int n = (y * 16 + x) * 4; sourcePixels[n] = sourcePixels[n + 1] = sourcePixels[n + 2] = sourcePixels[n + 3] = 255; }
            var sourcePng = new WriteableBitmap(16, 16, 96, 96, PixelFormats.Bgra32, null); sourcePng.WritePixels(new Int32Rect(0, 0, 16, 16), sourcePixels, 16 * 4, 0);
            var pngEncoder = new PngBitmapEncoder(); pngEncoder.Frames.Add(BitmapFrame.Create(sourcePng));
            using var pngStream = new MemoryStream(); pngEncoder.Save(pngStream);
            var pngProfile = new Profile { Style = CrosshairStyle.Custom, AssetType = "png", Asset = Convert.ToBase64String(pngStream.ToArray()) };
            var trimmedPng = (BitmapSource)AssetLoader.Load(pngProfile); Assert(trimmedPng.PixelWidth == 4 && trimmedPng.PixelHeight == 6, "Transparent PNG padding was not trimmed");
            Assert(Storage.Import(Storage.Export(pngProfile)).Asset == pngProfile.Asset, "Embedded PNG was not preserved in profile JSON");
            log.Add("PASS: transparent PNG import, alpha crop, and embedded profile round-trip");
            foreach (var json in new[] { "{\"Opacity\":2}", "{\"Color\":\"oops\"}", "{\"Style\":900}", "CH1:!" }) { bool failed = false; try { Storage.Import(json); } catch { failed = true; } Assert(failed, "Invalid profile accepted"); }
            var svg = new Profile { AssetType = "svg", Asset = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg><path d='M0 5 L5 0 L10 5' fill='none' stroke='#00ff00'/></svg>")) }; AssetLoader.Load(svg);
            svg.Asset = Convert.ToBase64String(Encoding.UTF8.GetBytes("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///test'>]><svg>&x;</svg>"));
            bool blocked = false; try { AssetLoader.Load(svg); } catch (System.Xml.XmlException) { blocked = true; } Assert(blocked, "DTD was accepted");
            log.Add("PASS: invalid profiles and external SVG DTD rejected");
            foreach (var key in new[] { "LabelFont", "BodyFont", "NumberFont" })
            {
                var typeface = new Typeface((FontFamily)Application.Current.FindResource(key), FontStyles.Normal, key == "LabelFont" ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
                Assert(typeface.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.ToString().Contains("Assets/Fonts", StringComparison.OrdinalIgnoreCase), "Embedded font failed: " + key);
            }
            log.Add("PASS: all three embedded OFL fonts resolve without system fallback");
            var vm = new EditorViewModel(new Settings { OnboardingComplete = true, ReduceMotion = true }, false);
            Assert(vm.Profiles.Count == 0, "Initial draft unexpectedly saved"); vm.Save(); Assert(vm.Profiles.Count == 1, "Save profile failed");
            double original = vm.Selected.Length; vm.History.Begin(); vm.Selected.Length = 19; vm.Selected.Length = 27; vm.History.End(); vm.History.Undo(); Assert(vm.Selected.Length == original, "Undo gesture"); vm.History.Redo(); Assert(vm.Selected.Length == 27, "Redo gesture"); vm.History.Undo();
            vm.Selected.Style = CrosshairStyle.Circle; vm.History.Undo(); Assert(vm.Selected.Style == CrosshairStyle.Cross, "Undo shape");
            Assert(ScrubField.DragValue(10, 10, 1, ModifierKeys.Shift, 0, 100) == 11, "Shift precision"); Assert(ScrubField.DragValue(10, 10, 1, ModifierKeys.Control, 0, 100) == 100, "Ctrl clamp"); Assert(ScrubField.DragValue(10, -30, 1, ModifierKeys.None, 0, 100) == 0, "Lower clamp");
            var green = Controls.ColorPicker.Hsv(120, 1, 1); Assert(green.G == 255 && green.R == 0 && green.B == 0, "HSV conversion");
            log.Add("PASS: draft/save, grouped undo/redo, shape undo, scrub modifiers/clamping, HSV");
            var foreground = GetForegroundWindow(); overlay = new OverlayWindow(); overlay.Apply(vm.Selected); overlay.Show(); var hwnd = new WindowInteropHelper(overlay).Handle;
            Assert((Native.GetWindowLong(hwnd, Native.GwlExStyle) & Native.OverlayStyles) == Native.OverlayStyles, "Overlay styles"); Assert(GetForegroundWindow() == foreground, "Overlay stole focus"); overlay.Toggle(); overlay.Toggle();
            log.Add("PASS: overlay HWND flags, no activation, toggle lifecycle");
            editor = new EditorWindow(vm, overlay); var content = (FrameworkElement)editor.Content;
            editor.WindowState = WindowState.Minimized; editor.PrepareTrayRestore();
            Assert(editor.WindowState == WindowState.Normal, "Minimized editor state was not prepared for tray restore");
            Snapshot(content, "initial-layout", 1100, 700);
            var slider = Find<Slider>(editor).First();
            slider.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
            slider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, 3d); slider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, 4d);
            slider.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0, 0, false) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
            Assert(vm.Selected.Thickness == 4, "Slider binding did not update profile"); vm.History.Undo(); Assert(vm.Selected.Thickness == 2, "Slider gesture not grouped");
            var boundPicker = Find<Controls.ColorPicker>(editor).First(); var hex = Find<TextBox>(boundPicker).First();
            hex.Text = "#112233"; Assert(vm.Selected.Color == "#112233", "Hex input binding"); hex.Text = "#GGGGGG"; Assert(vm.Selected.Color == "#112233", "Invalid hex changed profile"); vm.History.Undo();
            editor.Stage.Apply(vm.Selected, true); Assert(!editor.Stage.Crosshair.HasAnimatedProperties, "Reduce motion did not suppress preview animation");
            log.Add("PASS: actual slider routed gesture/binding, hex control validation, reduce-motion suppression");
            vm.Selected.Length = 33; editor.BeginCompare(); Assert(editor.Stage.Crosshair.Profile!.Length == original, "Compare baseline"); editor.EndCompare(); Assert(editor.Stage.Crosshair.Profile == vm.Selected && vm.Selected.Length == 33, "Compare restore"); vm.History.Undo();
            log.Add("PASS: hold-compare baseline and restore without modifying profile");
            foreach (var theme in new[] { "Optic", "Paper", "Signal" })
            {
                vm.Settings.Theme = theme; editor.RefreshAppearance(); editor.Navigate("Editor"); Snapshot(content, "theme-" + theme.ToLowerInvariant(), 1100, 700);
                editor.Navigate("Settings"); Snapshot(content, "settings-" + theme.ToLowerInvariant(), 1100, 700);
            }
            vm.Settings.Theme = "Optic"; editor.RefreshAppearance();
            vm.ApplyPreset(presets.First(p => p.Name == "White Micro Cross")); editor.Stage.Apply(vm.Selected); Snapshot(content, "white-crosshair", 1100, 700);
            vm.ApplyPreset(presets[0]); editor.Stage.Apply(vm.Selected);
            foreach (var page in new[] { "Editor", "Presets", "Profiles", "Hotkeys" }) { editor.Navigate(page); Snapshot(content, page.ToLowerInvariant() + "-preview", 1100, 700); }
            editor.Navigate("Editor"); foreach (var section in Find<SectionHeader>(editor).ToList()) section.Expanded = true; Snapshot(content, "inspector-expanded", 1100, 700);
            editor.Navigate("Editor"); var sections = Find<SectionHeader>(editor).ToList(); sections[0].Expanded = false; sections[1].Expanded = true; Snapshot(content, "color-inspector", 1100, 700);
            var picker = new Controls.ColorPicker { Color = "#FFB000", Width = 280 }; picker.SetRecent(new[] { "#00FF66", "#FFFFFF", "#FFB000" }); Snapshot(picker, "color-picker", 280, 260);
            Snapshot(content, "editor-compact", 980, 620);
            vm.Settings.Language = "en-US"; editor.RefreshAppearance(); Snapshot(content, "editor-english", 1100, 700); vm.Settings.Language = "id-ID"; editor.RefreshAppearance();
            log.Add("PASS: all themes, all navigation pages, expanded inspector, picker, compact and EN layout rendered");
            var hud = new MiniHudWindow(vm); Snapshot((FrameworkElement)hud.Content, "mini-hud", 280, 160); hud.Close();
            var tray = new TrayPopupWindow(vm, overlay, () => { }, () => { }); Snapshot((FrameworkElement)tray.Content, "tray-popup", 280, 320); tray.Close();
            var onboarding = new OnboardingWindow(editor, vm); for (int i = 0; i < 3; i++) { onboarding.GoToStep(i); Snapshot((FrameworkElement)onboarding.Content, "onboarding-" + (i + 1), 460, 440); } onboarding.Close();
            log.Add("PASS: tray restore, Mini HUD, custom tray, all three onboarding steps rendered");
            Assert(!editor.ApplyKeys(8, 8, 10), "Duplicate hotkey accepted");
            var id = new ResourceDictionary { Source = new Uri("Resources/Strings.id-ID.xaml", UriKind.Relative) }; var en = new ResourceDictionary { Source = new Uri("Resources/Strings.en-US.xaml", UriKind.Relative) }; Assert(id.Keys.Cast<object>().OrderBy(x => x.ToString()).SequenceEqual(en.Keys.Cast<object>().OrderBy(x => x.ToString())), "Localization keys differ");
            log.Add("PASS: hotkey duplicate rejection and ID/EN resource parity");
            editor.Exiting = true; editor.Close(); editor = null; overlay.Close(); overlay = null; vm.Stop();
            File.WriteAllLines("artifacts/self-test.txt", log); return 0;
        }
        catch (Exception ex) { log.Add("FAIL: " + ex); File.WriteAllLines("artifacts/self-test.txt", log); return 1; }
        finally { if (editor != null) { editor.Exiting = true; editor.Close(); } overlay?.Close(); }
    }
}

