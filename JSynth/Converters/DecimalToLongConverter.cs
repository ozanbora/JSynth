using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JSynth.Converters
{
    internal class DecimalToLongConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : value is not long ? throw new InvalidCastException() : (object)(decimal)(long)value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : value is not decimal ? throw new InvalidOperationException() : (object)(long)(decimal)value;
        }

    }
}
