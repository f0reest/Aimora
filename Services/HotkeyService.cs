using System;
using System.Linq;
using System.Windows.Interop;
namespace CrosshairApp.Services;
public sealed class HotkeyService : IDisposable
{
    private readonly HwndSource source;
    private int[] registered = Array.Empty<int>();
    public event Action<int>? Invoked;
    public bool HasBindings => registered.Length == 3;
    public HotkeyService(IntPtr hwnd) { source = HwndSource.FromHwnd(hwnd); source.AddHook(Hook); }
    public bool Apply(int toggle, int previous, int next)
    {
        var keys = new[] { toggle, previous, next };
        if (keys.Any(k => k < 1 || k > 12) || keys.Distinct().Count() != 3) return false;
        var old = registered;
        for (int i = 0; i < 3; i++) Native.UnregisterHotKey(source.Handle, i + 1);
        registered = Array.Empty<int>();
        for (int i = 0; i < 3; i++)
        {
            if (Native.RegisterHotKey(source.Handle, i + 1, 0x4003, (uint)(0x6F + keys[i]))) continue;
            for (int j = 0; j < 3; j++) Native.UnregisterHotKey(source.Handle, j + 1);
            if (old.Length == 3) { bool restored = true; for (int j = 0; j < 3; j++) restored &= Native.RegisterHotKey(source.Handle, j + 1, 0x4003, (uint)(0x6F + old[j])); if (restored) registered = old; }
            return false;
        }
        registered = keys; return true;
    }
    private IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) { if (msg == 0x312 && w.ToInt32() is >= 1 and <= 3) { Invoked?.Invoke(w.ToInt32()); handled = true; } return IntPtr.Zero; }
    public void Dispose() { for (int i = 0; i < 3; i++) Native.UnregisterHotKey(source.Handle, i + 1); source.RemoveHook(Hook); }
}

