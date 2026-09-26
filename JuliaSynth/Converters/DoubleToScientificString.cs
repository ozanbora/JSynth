using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JuliaSynth.Converters
{
    internal class DoubleToScientificString : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Shortest text that round-trips to the same double, e.g. 0 or 3.4028234663852886E+38.
            return value == null ? null : string.Format(culture, "{0:R}", value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return double.Parse((string)value!, culture);
        }

    }
}
