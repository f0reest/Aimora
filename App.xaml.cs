using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CrosshairApp.Services;
using CrosshairApp.ViewModels;
using CrosshairApp.Views;
using Forms = System.Windows.Forms;
namespace CrosshairApp;
public partial class App : Application
{
    private Mutex? mutex;
    private EditorViewModel? vm;
    private EditorWindow? editor;
    private OverlayWindow? overlay;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private TrayPopupWindow? trayPopup;
    private HotkeyService? hotkeys;
    private readonly DispatcherTimer compatibility = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool warned;
    public bool HotkeysAvailable { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test")) { Shutdown(Diagnostics.SelfTest.Run()); return; }
        mutex = new Mutex(true, @"Local\AIMORA.Desktop", out bool first);
        if (!first) { MessageBox.Show(Appearance.Text("AlreadyRunning")); mutex.Dispose(); mutex = null; Shutdown(); return; }
        Settings settings;
        try { settings = Storage.Load(); }
        catch (Exception ex) { MessageBox.Show(Appearance.Text("SettingsError") + "\n" + Storage.Folder + "\n" + ex.Message); Shutdown(); return; }
        Appearance.ReduceMotion = settings.ReduceMotion; Appearance.Apply(settings.Theme, settings.Language);
        vm = new EditorViewModel(settings);
        overlay = new OverlayWindow { MonitorName = settings.Monitor }; overlay.Apply(vm.Selected); overlay.Show(); vm.SelectionChanged += overlay.Apply;
        editor = new EditorWindow(vm, overlay); MainWindow = editor;
        hotkeys = new HotkeyService(new WindowInteropHelper(editor).EnsureHandle());
        hotkeys.Invoked += id => { switch (id) { case 1: overlay.Toggle(); break; case 2: vm.Next(-1); break; case 3: vm.Next(1); break; } };
        if (!ApplyHotkeys(settings.ToggleKey, settings.PreviousKey, settings.NextKey)) vm.Status = Appearance.Text("HotkeyConflict");
        trayIcon = CreateTrayIcon(); tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "AIMORA", Visible = true };
        tray.MouseClick += (_, e) => Dispatcher.BeginInvoke(() =>
        {
            // Left click is idempotent: even a Windows double-click only restores
            // the editor twice. Right click owns the custom tray popup.
            if (e.Button == Forms.MouseButtons.Left)
            {
                trayPopup?.Close(); trayPopup = null; editor.OpenEditor(); return;
            }
            if (e.Button != Forms.MouseButtons.Right) return;
            if (trayPopup?.IsVisible == true) { trayPopup.Close(); trayPopup = null; return; }
            trayPopup = new TrayPopupWindow(vm, overlay, editor.OpenEditor, ExitApp);
            trayPopup.Closed += (_, _) => trayPopup = null;
            trayPopup.Show();
        });
        compatibility.Tick += (_, _) =>
        {
            bool detected = Native.SHQueryUserNotificationState(out int state) == 0 && state == 3;
            if (detected && !warned) { string message = Appearance.Text("Exclusive"); vm.Status = message; tray.ShowBalloonTip(6000, "AIMORA", message, Forms.ToolTipIcon.None); }
            warned = detected;
        };
        compatibility.Start(); if (!e.Args.Contains("--tray")) editor.Show();
    }
    public bool ApplyHotkeys(int toggle, int previous, int next)
    {
        if (hotkeys == null) return new[] { toggle, previous, next }.Distinct().Count() == 3 && new[] { toggle, previous, next }.All(x => x >= 1 && x <= 12);
        bool success = hotkeys.Apply(toggle, previous, next); HotkeysAvailable = hotkeys.HasBindings; return success;
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bitmap)) using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.5f))
        { g.DrawEllipse(pen, 4, 4, 8, 8); g.DrawLine(pen, 8, 0, 8, 5); g.DrawLine(pen, 8, 11, 8, 16); g.DrawLine(pen, 0, 8, 5, 8); g.DrawLine(pen, 11, 8, 16, 8); }
        var handle = bitmap.GetHicon(); try { using var icon = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)icon.Clone(); } finally { DestroyIcon(handle); }
    }
    public void ExitApp() { if (editor != null) editor.Exiting = true; Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        compatibility.Stop(); vm?.Stop(); hotkeys?.Dispose(); tray?.Dispose(); trayIcon?.Dispose(); mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e);
    }
}

