using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JuliaSynth.Controls;

/// <summary>
/// How values from a run are shown.
/// </summary>
public static class ResultStyles
{
    /// <summary>
    /// Errors and "not run" in italics, values upright.
    /// </summary>
    public static IValueConverter ItalicWhenTrue { get; } = new FuncValueConverter<bool, FontStyle>(error => error ? FontStyle.Italic : FontStyle.Normal);

    /// <summary>
    /// The hint shown before a run is dimmer than a value.
    /// </summary>
    public static IValueConverter DimWhenFalse { get; } = new FuncValueConverter<bool, double>(hasResult => hasResult ? 1.0 : 0.7);
}
