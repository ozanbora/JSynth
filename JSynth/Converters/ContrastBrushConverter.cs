using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JSynth.Converters
{
    /// <summary>
    /// Picks black or white, whichever is more legible on top of the given background color.
    /// </summary>
    public class ContrastBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Color color)
            {
                return null;
            }

            // Black has the better contrast ratio above a relative luminance of about 0.18.
            return ColorContrast.Luminance(color) > 0.179 ? Brushes.Black : Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
