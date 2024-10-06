using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JuliaSynth.Converters
{
    internal class DoubleToScientificString : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : string.Format(culture, "{0:e17}", value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return double.Parse((string)value!, culture);
        }

    }
}
