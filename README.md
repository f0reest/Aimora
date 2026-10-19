# AIMORA — Optic

Aplikasi crosshair Windows 10/11, C# .NET 8 WPF, satu project, tanpa telemetry
atau koneksi internet saat runtime. Default Optic; Paper dan Signal tersedia
di Settings. Source lengkap, font dan lisensi disertakan dalam arsip source.

## Build / publish

Pasang .NET 8 SDK Windows, lalu:
~~~powershell
dotnet build -c Release
dotnet run -c Release
dotnet publish CrosshairApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish/aimora
~~~

EXE mandiri: publish/aimora/AIMORA.exe. Keluar dari versi lama lewat tray
sebelum menjalankan versi baru. Single-file mengekstrak native runtime saat
diperlukan; komputer tujuan tidak memerlukan instalasi .NET. Restore SDK pertama
dapat membutuhkan internet. Jangan aktifkan trimming WPF.

## Penggunaan

1. Onboarding memilih monitor, preset, dan hotkey. Setup dapat dibuka ulang.
2. Klik preset untuk membuat draft; Ctrl+S / Simpan profil menyimpannya.
   Profiles menyediakan rename, duplicate, delete, import/export JSON dan
   share-code CH1:. Profil terakhir boleh dihapus. Maksimal 100 profil.
3. Edit di inspector. Drag angka, Shift untuk langkah halus, Ctrl langkah besar.
   Klik untuk mengetik; Enter menerapkan, Escape membatalkan. Reset muncul bila
   nilai berbeda dari default. Ctrl+Z / Ctrl+Y untuk undo/redo.
4. Tahan Compare dengan mouse atau Space saat tombol fokus untuk melihat
   baseline ketika profil dipilih. Lepas untuk kembali ke edit.
5. Stage memiliki penggaris piksel, grid, zoom 1×/4×/8× dan latar rumput, beton,
   langit, salju, gelap. Tekstur prosedural lokal; custom PNG/JPEG/BMP maksimal
   8 MB / 4096×4096. Gambar latar custom hanya berlaku selama sesi.
6. Color picker memiliki SV/hue, hex, warna terakhir dan eyedropper.
   Area SV/hue menerima panah keyboard; Escape membatalkan eyedropper.
   Untuk crosshair gambar, tekan Impor crosshair PNG di bagian Bentuk atau tarik
   satu file PNG ke preview. Transparansi dipertahankan, padding transparan
   dipotong otomatis, lalu data gambar disematkan ke profil.
7. Settings mengatur tema, bahasa Indonesia/English, reduce motion dan monitor.
   Ukuran overlay dalam piksel fisik; zoom hanya memengaruhi preview.
8. Mini HUD 280×160 dibuka melalui rail bawah atau Settings. Klik tray membuka
   kembali editor; klik kanan membuka popup WPF berisi profil, toggle, editor dan
   keluar. X menutup editor ke tray.
9. Start with Windows memakai HKCU Run. Aktifkan dari lokasi EXE permanen;
   jika dipindah, nonaktifkan lalu aktifkan kembali.

| Default shortcut | Aksi |
| --- | --- |
| Ctrl+Alt+F8 | Toggle overlay |
| Ctrl+Alt+F9 | Profil sebelumnya |
| Ctrl+Alt+F10 | Profil berikutnya |
| Ctrl+S | Simpan profil |
| Ctrl+Z / Ctrl+Y | Undo / Redo |

Layar Hotkeys dapat mengganti tombol F1–F12, modifier Ctrl+Alt tetap.
Duplikasi/bentrok ditolak; registrasi lama dicoba dipulihkan jika apply gagal.

## Penyimpanan

%AppData%/AIMORA/settings.json menyimpan profil, tema, bahasa, monitor,
hotkey dan preferensi. Edit profil tersimpan di-debounce 600 ms. Draft harus
disimpan dengan Ctrl+S. Penulisan memakai file sementara lalu replacement.
Settings rusak tidak ditimpa; cadangkan dan perbaiki/pindahkan untuk reset.
Share-code adalah JSON Base64, bukan enkripsi. Aset crosshair ikut dalam profil.
Pada peluncuran pertama setelah pembaruan nama, pengaturan lama dari
%AppData%/CrosshairApp dimigrasikan tanpa menghapus berkas asal.

## PNG / SVG

Aset crosshair maksimal 2 MB; PNG maksimal 2048×2048. SVG mendukung subset
path geometry WPF, circle, ellipse, rect, line, polygon, polyline, grup,
fill/stroke sederhana. Tidak mendukung CSS, transform, text, filter, mask,
clip-path, gradient, scripts, referensi eksternal atau DTD. Bounding box konten
dipakai, bukan padding viewBox. Flatten SVG atau ekspor PNG untuk hasil presisi.
Warna/outline/dynamic color berlaku pada bentuk bawaan; aset memakai warna asli.
Rotasi, ukuran dan opacity juga berlaku pada aset.

## Performa / kompatibilitas

Overlay transparan 640×640, click-through, topmost, toolwindow, no-activate.
Rendering retained-mode WPF hanya diubah saat perlu. Tidak ada loop render idle;
pemeriksaan indikasi fullscreen setiap 3 detik. Efek pulse/dynamic color opt-in
memakai timer 20 Hz dan berhenti ketika overlay disembunyikan. Reduce motion
dan pengaturan animasi Windows meniadakan pulse dan transisi.

Dynamic color memilih hitam/putih berdasarkan luminance pusat layar. Windows
10 2004+ memakai capture exclusion agar tidak membaca overlay sendiri; overlay
juga dapat tidak muncul dalam rekaman. Bila tidak didukung, sampling bergeser
324 px ke kanan. Konten protected/hardware overlay dapat tidak terbaca.
Preview menggunakan luminance pusat latar uji, dan mendukung pulse.

Utamakan Windowed / Borderless. Overlay WPF tidak dijamin terlihat pada true
exclusive fullscreen. SHQueryUserNotificationState memberi indikasi, bukan
deteksi definitif, dengan saran mengganti ke Borderless. Fullscreen Optimized
bergantung pada compositor, driver dan game.

Hook/injection adalah opsi terpisah, disabled dan tidak tersedia di build ini.
Tidak ada injector yang dikirim. Lihat docs/EXPERIMENTAL.md untuk arsitektur dan
risiko anti-cheat VAC/EAC/BattlEye/Vanguard. Pergantian profil otomatis berdasarkan
proses game belum diimplementasikan.

## Struktur proyek

~~~text
CrosshairApp.csproj / app.manifest
App.xaml / App.xaml.cs          Resource, lifecycle, tray, hotkey
Models/Profile.cs              Model observable dan validasi
ViewModels/EditorViewModel.cs   Draft/profil, persistence, binding
ViewModels/EditHistory.cs      Undo/redo terkelompok
Controls/                      ScrubField, ColorPicker, SegmentedControl,
                               ToggleSquare, SectionHeader, ParameterRow,
                               TrackedLabel, Ui, Templates.xaml
Themes/                        Theme.Optic.xaml, Paper, Signal
Resources/                     Icons.xaml, Strings.id-ID.xaml, en-US
Assets/Fonts/                  Tiga font embedded + lisensi OFL
Views/                         EditorWindow.xaml + .cs, StageView,
                               CrosshairVisual, OverlayWindow, MiniHudWindow,
                               TrayPopupWindow, OnboardingWindow
Services/                      Win32, storage, assets, hotkeys, appearance
Diagnostics/SelfTest.cs         Smoke test dan snapshot
presets.json                   24 preset lengkap
docs/UI-DESIGN.md               Token, wireframe, keputusan desain
docs/EXPERIMENTAL.md            Arsitektur backend eksperimental
~~~

## Verifikasi

~~~powershell
dotnet run -c Release -- --self-test
Get-Content artifacts/self-test.txt
~~~

Memeriksa preset/JSON/share-code, validasi aset, font embedded, draft/save,
undo/redo, gesture slider routed dan binding, hex, HSV, Compare, reduce motion,
HWND/fokus, hotkey duplikat dan parity resource bahasa. Snapshot mencakup tiga
tema, semua layar, ukuran minimum, English, HUD, tray dan onboarding.

Pengujian game nyata dan monitor fisik belum dilakukan. Periksa click-through
sambil mengetik di aplikasi lain; monitor 100/150/200% termasuk koordinat negatif;
restart/persistence; startup Windows; hotkey bentrok; serta CPU/GPU saat idle.
UI utama bilingual; error teknis parser/OS dapat tetap berbahasa Inggris.
