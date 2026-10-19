# Experimental DirectX backend — DISABLED

This distribution only renders a normal out-of-process WPF/Win32 overlay. The
disabled UI option is a capability placeholder, not a working injector. Enabling
it in a JSON file has no effect. No DLL injection, game-memory access, hook,
anti-cheat bypass, driver, or privilege escalation is included.

## Proposed separate architecture

If developed for an explicitly authorized test application, use a separate native
C++ project and distribution. Keep it out of this single-project desktop build.

1. A backend contract receives an immutable crosshair drawing description,
   visibility state, physical pixel coordinates, and color. The editor remains
   independent of rendering technology.
2. The default `WindowOverlayBackend` owns the WPF HWND and never enters another
   process. It is the only backend implemented here (`OverlayWindow`).
3. An optional native backend would need renderer-specific adapters for D3D11
   and D3D12, device and swap-chain lifecycle management, resizing, state
   preservation, synchronization, and clean shutdown. These are not equivalent
   to merely changing a window flag.
4. IPC would transfer a bounded, versioned rendering description over a local
   authenticated channel. Do not transfer arbitrary executable commands.
5. Use an application-owned test renderer or an officially supported plugin API
   first. Any external-process injection mechanism would require separate code,
   explicit consent, compatibility testing, and review of the game's policy.

VAC, EAC, BattlEye, and Vanguard may treat hooks/injection as tampering and may
kick or ban an account. No allowlist or “safe” guarantee is implied. Ordinary
overlays can also be prohibited by individual games. This project does not
attempt to hide from security software. Prefer Borderless Windowed.

Bahasa Indonesia: backend eksperimental hanya rancangan, tidak dapat diaktifkan
dalam build ini. Hook dapat menyebabkan kick/ban. Gunakan overlay biasa dan mode
Borderless Windowed.
