using Avalonia.Media;

namespace JuliaSynth.Controls;

/// <summary>
/// Ways to cut text short for use in XAML.
/// </summary>
public static class Trimming
{
    /// <summary>
    /// Ends cut text with three dots: Noto Sans Math, the font of the captions, has no … character.
    /// </summary>
    public static TextTrimming Dots { get; } = new TextTrailingTrimming("...", isWordBased: false);
}
