using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace CrosshairApp.Services;

public static class Appearance
{
    private static ResourceDictionary? theme, language;
    public static bool ReduceMotion { get; set; }
    public static string CurrentTheme { get; private set; } = "Optic";
    public static string CurrentLanguage { get; private set; } = "id-ID";
    public static event Action? Changed;
    public static void Apply(string name, string culture)
    {
        CurrentTheme = name is "Paper" or "Signal" ? name : "Optic";
        CurrentLanguage = culture == "en-US" ? culture : "id-ID";
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (theme != null) dictionaries.Remove(theme);
        if (language != null) dictionaries.Remove(language);
        theme = new ResourceDictionary { Source = new Uri($"Themes/Theme.{CurrentTheme}.xaml", UriKind.Relative) };
        language = new ResourceDictionary { Source = new Uri($"Resources/Strings.{CurrentLanguage}.xaml", UriKind.Relative) };
        dictionaries.Add(theme); dictionaries.Add(language); Changed?.Invoke();
    }
    public static string Text(string key) => Application.Current.TryFindResource("S." + key) as string ?? key;
    public static void Reveal(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (ReduceMotion || !SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.2, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
}
