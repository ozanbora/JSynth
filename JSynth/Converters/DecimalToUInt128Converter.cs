using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JSynth.Converters
{
    internal class DecimalToUInt128Converter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : value is not UInt128 ? throw new InvalidCastException() : (object)(decimal)(UInt128)value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? null : value is not decimal ? throw new InvalidOperationException() : (object)(UInt128)(decimal)value;
        }

    }
}
