using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrosshairApp.Services;

// Deterministic offline test surfaces, not screenshots or downloaded stock imagery.
public static class TestTextures
{
    private static readonly Dictionary<string, BitmapSource> cache = new();
    public static BitmapSource Get(string kind)
    {
        if (cache.TryGetValue(kind, out var existing)) return existing;
        const int size = 512; var data = new byte[size * size * 4]; var rng = new Random(34);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            double noise = rng.NextDouble() - .5, n = noise * 22;
            double wave = Math.Sin(x * .041 + Math.Sin(y * .015) * 2) * Math.Cos(y * .018) * 8;
            (double r, double g, double b) = kind switch
            {
                "Grass" => (43 + n + wave, 53 + n + wave, 29 + n),
                "Sky" => (114 + y * .11 + wave, 144 + y * .09 + wave, 158 + y * .08 + wave),
                "Snow" => (220 + n / 4 + wave, 224 + n / 4 + wave, 219 + n / 4 + wave),
                "Dark" => (12 + n / 8, 14 + n / 8, 14 + n / 8),
                _ => (77 + n + wave, 79 + n + wave, 73 + n + wave)
            };
            if (kind == "Grass" && (x + y * 3) % 19 == 0) { r += 14; g += 20; b += 4; }
            if (kind == "Concrete" && (x % 255 < 2 || y % 255 < 2)) { r -= 18; g -= 18; b -= 18; }
            int i = (y * size + x) * 4; data[i] = (byte)Math.Clamp(b, 0, 255); data[i + 1] = (byte)Math.Clamp(g, 0, 255); data[i + 2] = (byte)Math.Clamp(r, 0, 255); data[i + 3] = 255;
        }
        var image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, data, size * 4); image.Freeze(); cache[kind] = image; return image;
    }
    public static BitmapSource Load(string path)
    {
        if (new FileInfo(path).Length > 8_000_000) throw new FormatException(Appearance.Text("ImageTooLarge"));
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        if (decoder.Frames[0].PixelWidth > 4096 || decoder.Frames[0].PixelHeight > 4096) throw new FormatException(Appearance.Text("ImageTooLarge"));
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.StreamSource = stream; image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = Math.Min(1600, decoder.Frames[0].PixelWidth); image.EndInit(); image.Freeze(); return image;
    }
}
