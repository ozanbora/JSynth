using Avalonia.Data;
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
            // An emptied box keeps the value it had; a number too large for long is an error on the box.
            if (value is not decimal number)
            {
                return BindingOperations.DoNothing;
            }

            try
            {
                return (long)number;
            }
            catch (OverflowException e)
            {
                return new BindingNotification(e, BindingErrorType.Error);
            }
        }

    }
}
