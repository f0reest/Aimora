using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CrosshairApp.Models;

namespace CrosshairApp.Services;

public sealed class Settings
{
    public List<Profile> Profiles { get; set; } = new();
    public int SelectedIndex { get; set; }
    public string Monitor { get; set; } = "";
    public bool MinimizeToTray { get; set; } = true;
    public string Theme { get; set; } = "Optic";
    public string Language { get; set; } = "id-ID";
    public bool ReduceMotion { get; set; }
    public bool OnboardingComplete { get; set; }
    public int ToggleKey { get; set; } = 8;
    public int PreviousKey { get; set; } = 9;
    public int NextKey { get; set; } = 10;
    public string TestBackground { get; set; } = "Concrete";
    public System.Collections.Generic.List<string> RecentColors { get; set; } = new();
}

public static class Storage
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIMORA");
    private static readonly string LegacyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrosshairApp");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static List<Profile> Presets()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CrosshairApp.presets.json")!;
        var profiles = JsonSerializer.Deserialize<List<Profile>>(stream)!;
        foreach (var p in profiles) p.Validate();
        return profiles;
    }
    public static Settings Load()
    {
        var path = Path.Combine(Folder, "settings.json");
        bool migrate = !File.Exists(path) && File.Exists(Path.Combine(LegacyFolder, "settings.json"));
        if (migrate) path = Path.Combine(LegacyFolder, "settings.json");
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 40_000_000) throw new FormatException("Settings too large.");
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new FormatException("Invalid settings.");
        if (s.Profiles == null || s.Profiles.Count > 100) throw new FormatException("Maximum 100 profiles.");
        foreach (var p in s.Profiles) p.Validate();
        s.RecentColors ??= new(); s.RecentColors = s.RecentColors.Where(c => c != null && System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")).Take(8).ToList();
        // Preserve existing users' profiles while moving the product identity.
        if (migrate) Save(s);
        return s;
    }
    public static void Save(Settings s)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
        // Atomic replacement leaves the previous settings intact if writing fails.
        File.Move(tmp, path, true);
    }
    public static string Export(Profile p) { p.Validate(); return JsonSerializer.Serialize(p, Options); }
    public static Profile Import(string text)
    {
        if (text.Length > 4_000_000) throw new FormatException("Profile too large.");
        if (text.StartsWith("CH1:")) text = Encoding.UTF8.GetString(Convert.FromBase64String(text[4..]));
        var p = JsonSerializer.Deserialize<Profile>(text) ?? throw new FormatException("Invalid profile.");
        p.Validate(); return p;
    }
    public static string Share(Profile p) => "CH1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Export(p)));
}

