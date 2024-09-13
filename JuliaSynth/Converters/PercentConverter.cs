using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JuliaSynth.Converters
{
    public class PercentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : string.Format(culture, "{0:P0}", value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
