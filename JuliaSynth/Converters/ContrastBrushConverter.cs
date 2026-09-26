using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JuliaSynth.Converters
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

            // WCAG relative luminance; black has the better contrast ratio above ~0.18.
            static double Linear(byte c)
            {
                double s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            double luminance = (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
            return luminance > 0.179 ? Brushes.Black : Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
