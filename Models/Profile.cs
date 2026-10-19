using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace CrosshairApp.Models;

public enum CrosshairStyle { Cross, CrossDot, Dot, Circle, TShape, XShape, Chevron, Square, Custom }

public sealed class Profile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
    private string name = "My crosshair", color = "#00FF66", outlineColor = "#000000", asset = "", assetType = "";
    private CrosshairStyle style;
    private double opacity = 1, thickness = 2, length = 12, gap = 4, dotSize = 3, rotation, outlineThickness = 1, offsetX, offsetY, imageSize = 48;
    private bool outline = true, dynamicColor, pulse;
    public string Name { get => name; set => Set(ref name, value); }
    public CrosshairStyle Style { get => style; set => Set(ref style, value); }
    public string Color { get => color; set => Set(ref color, value); }
    public string OutlineColor { get => outlineColor; set => Set(ref outlineColor, value); }
    public double Opacity { get => opacity; set => Set(ref opacity, value); }
    public double Thickness { get => thickness; set => Set(ref thickness, value); }
    public double Length { get => length; set => Set(ref length, value); }
    public double Gap { get => gap; set => Set(ref gap, value); }
    public double DotSize { get => dotSize; set => Set(ref dotSize, value); }
    public double Rotation { get => rotation; set => Set(ref rotation, value); }
    public double OutlineThickness { get => outlineThickness; set => Set(ref outlineThickness, value); }
    public double OffsetX { get => offsetX; set => Set(ref offsetX, value); }
    public double OffsetY { get => offsetY; set => Set(ref offsetY, value); }
    public double ImageSize { get => imageSize; set => Set(ref imageSize, value); }
    public bool Outline { get => outline; set => Set(ref outline, value); }
    public bool DynamicColor { get => dynamicColor; set => Set(ref dynamicColor, value); }
    public bool Pulse { get => pulse; set => Set(ref pulse, value); }
    public string Asset { get => asset; set => Set(ref asset, value); }
    public string AssetType { get => assetType; set => Set(ref assetType, value); }
    public Profile Clone() => JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new FormatException("Nama / name: 1–80 characters.");
        if (!Enum.IsDefined(Style)) throw new FormatException("Unknown style.");
        foreach (var c in new[] { Color, OutlineColor })
            if (c == null || !System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")) throw new FormatException("Color: #RRGGBB");
        Range(Opacity, 0, 1); Range(Thickness, .5, 20); Range(Length, 0, 100); Range(Gap, 0, 100);
        Range(DotSize, 0, 40); Range(Rotation, -180, 180); Range(OutlineThickness, 0, 10);
        Range(OffsetX, -8000, 8000); Range(OffsetY, -8000, 8000); Range(ImageSize, 4, 256);
        if (Asset == null || Asset.Length > 3_000_000) throw new FormatException("Asset too large.");
        if (Asset.Length > 0) Services.AssetLoader.Load(this);
    }
    private static void Range(double v, double min, double max)
    { if (!double.IsFinite(v) || v < min || v > max) throw new FormatException($"Value must be {min}–{max}."); }
}
