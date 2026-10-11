using Avalonia.Data;
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
            // An emptied box keeps the value it had; a number too large for UInt128 is an error on the box.
            if (value is not decimal number)
            {
                return BindingOperations.DoNothing;
            }

            try
            {
                return (UInt128)number;
            }
            catch (OverflowException e)
            {
                return new BindingNotification(e, BindingErrorType.Error);
            }
        }

    }
}
