using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ASPAssistant.App;

public static class ThemeManager
{
    private static readonly ResourceDictionary Palette = new()
    {
        Source = new Uri("/ASPAssistant.App;component/Resources/Themes/Dark.xaml", UriKind.Relative)
    };
    private static readonly Dictionary<string, SolidColorBrush> ColorSources = new();

    static ThemeManager()
    {
        foreach (var key in Palette.Keys.Cast<string>().ToArray())
            if (Palette[key] is SolidColorBrush brush)
            {
                var source = brush.Clone();
                ColorSources.Add(key, source);
                var liveBrush = new SolidColorBrush();
                BindingOperations.SetBinding(liveBrush, SolidColorBrush.ColorProperty,
                    new Binding(nameof(SolidColorBrush.Color)) { Source = source, Mode = BindingMode.OneWay });
                Palette[key] = liveBrush;
            }
    }

    public static bool IsLight { get; private set; }

    public static void Apply(bool isLight)
    {
        var colors = new ResourceDictionary
        {
            Source = new Uri($"/ASPAssistant.App;component/Resources/Themes/{(isLight ? "Light" : "Dark")}.xaml",
                UriKind.Relative)
        };
        foreach (string key in colors.Keys)
        {
            if (colors[key] is SolidColorBrush brush)
                ColorSources[key].Color = brush.Color;
            else
                Palette[key] = colors[key];
        }
        Application.Current.Resources.MergedDictionaries[0] = Palette;
        IsLight = isLight;
    }
}
