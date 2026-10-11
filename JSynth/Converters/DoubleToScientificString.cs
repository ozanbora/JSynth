using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JSynth.Converters
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
            // A typo is an error on the field, and the value stays as it was.
            return double.TryParse(value as string, NumberStyles.Float, culture, out double number)
                ? number
                : new BindingNotification(new FormatException($"'{value}' isn't a number."), BindingErrorType.Error);
        }

    }
}
