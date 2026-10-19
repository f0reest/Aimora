# UI — Optic, Paper, Signal

## Arah desain

Optic default: instrumen bidik dengan stage dominan, rail sempit, angka
monospaced, dan satu aksen amber. Paper dan Signal memakai struktur yang sama.
Tidak ada blur, shadow, emoji, kartu dashboard atau animasi dekoratif.

## Token warna

| Resource key | Optic | Paper | Signal |
| --- | --- | --- | --- |
| CanvasBrush | #121414 | #EDEAE0 | #0A0A0A |
| PanelBrush | #1A1D1C | #E3DFD3 | #121212 |
| InputBrush | #202522 | #F5F2E9 | #1A1A1A |
| LineBrush | #2C312F | #B7B9B0 | #555555 |
| AccentBrush | #FFB000 | #E5482D | #B6FF3B |
| TextBrush | #D9DDD8 | #1B2A3A | #F4F4F4 |
| SecondaryBrush | #7E867F | #586471 | #AAAAAA |
| MutedBrush | #949D96 | #4D5966 | #B8B8B8 |
| AccentInkBrush | #121414 | #0F1820 | #0A0A0A |
| FocusBrush | #D9DDD8 | #1B2A3A | #F4F4F4 |

SecondaryBrush Optic mempertahankan brief untuk marka nonteks. MutedBrush yang
lebih terang dipakai pada label kecil. Kontras teks/panel: 12.36, 10.95, 17.03:1.
Label/panel: 6.08, 5.37, 9.44:1. Teks primary button: 10.09, 4.52, 16.36:1.
Kontras crosshair pada tekstur uji memang bervariasi sesuai pilihan pengguna.

## Tipografi, ukuran dan spacing

| Token / aturan | Semua tema |
| --- | --- |
| LabelFont | Barlow Condensed SemiBold |
| BodyFont | IBM Plex Sans Regular |
| NumberFont | JetBrains Mono Regular |
| LabelSize / BodySize / HeadingSize | 11 / 12 / 18 px |
| LabelTracking | uppercase +0.08 em |
| SpaceXS / SM / MD / LG / XL | 4 / 8 / 12 / 16 / 24 px |
| ControlHeight / ControlPadding | 32 / 10,6 |
| ShellWidth / ShellHeight | 1100 / 700 |
| RailWidth / InspectorWidth / TitleHeight | 64 / 320 / 40 |
| HudWidth / HudHeight | 280 / 160 |
| LineThickness / SliderStroke | 1 / 2 |
| IconSize / IconStroke | 16 / 1.5 |
| MotionMs | 150 ms cubic-out |
| ControlRadius | Optic 2, Paper 0, Signal 0 |
| FocusThickness | Optic 1, Paper 1, Signal 2 |

UI memakai WPF device-independent pixels; overlay memakai piksel fisik.
Ukuran 14 dan 28 tidak dipakai untuk menjaga maksimal tiga ukuran per layar.
TrackedLabel menggambar glyph dengan advance tambahan karena TextBlock WPF
tidak menyediakan letter-spacing. Font dan lisensi SIL OFL disematkan:
[Barlow](https://github.com/google/fonts/tree/main/ofl/barlowcondensed),
[IBM Plex](https://github.com/IBM/plex),
[JetBrains Mono](https://github.com/JetBrains/JetBrainsMono).

## Wireframe tiap layar

### Editor
~~~text
[mark / AIMORA]                         [Simpan profil] [min][max][close]
-----------------------------------------------------------------------
rail64 | UJI KONTRAS / nama [undo][redo][compare][eye] | inspector320
Editor | + penggaris piksel -----------------------+ | SHAPE v
Presets| |                                        | | [bentuk 5]
Profile| |        crosshair pada tekstur           | | [bentuk 4]
Hotkeys| |                                        | | THICKNESS 2
Setting| |                       [1x][4x][8x]      | | ---|-------
       | +----------------------------------------+ | LENGTH 12
       | [Rumput][Beton][Langit][Salju][Gelap][Custom]| GAP 4 / DOT 3
       | PRESET                                     | ROTATION 0
       | [aktif besar][patch][patch][patch] -->      | COLOR >
HUD    | nama + MINE/BUILT-IN                       | OUTLINE / BEHAVIOR >
-----------------------------------------------------------------------
status / validasi
~~~

Satu aksi utama persistent: Simpan profil. Stage tetap terlihat saat navigasi.
Color berisi SV/hue, hex, eyedropper, recents dan opacity. Outline berisi toggle,
warna dan width. Behavior berisi dynamic, pulse, offset, aset dan ukuran.

### Presets
~~~text
rail | stage tetap | PRESETS
     |             | [Semua][Bawaan][Milik saya]
     | strip hasil filter horizontal
     | [aktif156][112][120][128] -->
~~~
Klik membuat draft independen; built-in tidak ditimpa. Patch memakai renderer
yang sama dengan stage, dan ukuran tile aktif lebih besar.

### Profiles
~~~text
rail | stage tetap | PROFILES
     |             | daftar tersimpan atau empty state
     |             | "Belum ada profil. Edit preset lalu tekan Ctrl+S."
     |             | NAME [........................]
     |             | [Duplikat] [Hapus]
     |             | [Import] [Export]
     |             | [Salin kode] [Tempel kode]
~~~

### Hotkeys
~~~text
rail | stage tetap | HOTKEYS
     |             | TOGGLE   Ctrl+Alt [F8 v]
     |             | PREVIOUS Ctrl+Alt [F9 v]
     |             | NEXT     Ctrl+Alt [F10 v]
     |             | [Terapkan] / status
~~~

### Settings
~~~text
rail | stage tetap | SETTINGS
     |             | THEME [Optic][Paper][Signal]
     |             | LANGUAGE [Indonesia][English]
     |             | REDUCE MOTION [ON/OFF]
     |             | MONITOR [display v] [refresh]
     |             | MINIMIZE TO TRAY [ON/OFF]
     |             | START WITH WINDOWS [ON/OFF]
     |             | [Mini HUD] [Setup]
     |             | COMPATIBILITY >
~~~

### Mini HUD — 280×160
~~~text
[AIMORA]                         [close]
nama profil
              preview
Ctrl+Alt+F8                      ON/OFF
~~~
Always-on-top, tidak mengaktifkan diri saat dibuka. Status mencerminkan
ketersediaan hotkey, bukan deteksi game tertentu.

### Tray popup
~~~text
AIMORA
daftar profil tersimpan, scroll bila panjang
------------------------------------------
[Overlay ON/OFF]
[Buka editor]                    [Keluar]
~~~
WPF popup sesuai isi, dekat pointer dan dibatasi area kerja monitor.
Escape atau kehilangan fokus menutup popup.

### Onboarding
~~~text
SETUP                             [close]
==========  ----------  ----------
1: pilih monitor
2: pilih preset
3: atur tiga hotkey

[Previous]                 [Next / Finish]
~~~
Tiga garis, satu langkah aktif. Finish memvalidasi hotkey lalu menyimpan preset.
Setup dapat dibuka kembali dari Settings.

## Keputusan implementasi

- Template semua Button, TextBox, ComboBox, CheckBox, Slider dan ScrollBar
  menggunakan resource semantik. Tidak memakai chrome kontrol default.
- Ikon Geometry 16×16 stroke 1.5 px, fokus terlihat; Tab mengikuti urutan.
  Segmented control dan picker menerima panah keyboard.
- Scrub drag normal ×1, Shift ×0.1, Ctrl ×10; nilai dijepit ke rentang.
  Satu gesture slider menjadi satu undo. Reset hanya muncul jika berubah.
- Compare menampilkan baseline tanpa memodifikasi data.
- Gradient pada spectrum picker fungsional; tidak ada gradient dekoratif panel.
- Reveal preset menggunakan clip 150 ms; navigasi/section memakai opacity.
  Reduce motion dan setting animasi OS meniadakannya.
- Pulse opt-in; tidak ada animasi idle dekoratif.
- Renderer memakai DrawingContext dalam OnRender WPF retained-mode, berbagi kode
  dengan overlay. Tidak perlu dependensi SkiaSharp untuk geometri kecil ini.
- Tekstur prosedural menghindari dependensi foto/network; custom image tersedia.
- UI utama memakai resource ID/EN. Nama font/preset tetap sebagai proper name;
  error teknis parser/OS dapat berbahasa Inggris.

## Verifikasi

Self-test meliputi 24 preset, JSON/share-code, validasi aset, font embedded,
draft/save, undo/redo, actual routed slider gesture/binding, hex valid/invalid,
HSV, Compare, reduce motion, HWND click-through, fokus, hotkey duplikat dan
parity resource bahasa. Snapshot semua tema, layar, inspector, HUD, tray,
onboarding, minimum 980×620 dan English berada di artifacts/.

Ini tidak menggantikan uji game nyata, startup Windows, performa GPU dan beberapa
monitor fisik. Backend injection tidak diimplementasikan; lihat EXPERIMENTAL.md.

