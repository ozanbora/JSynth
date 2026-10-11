using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JSynth.Converters
{
    /// <summary>
    /// A number input's value or limit on its node: floats to 6 significant digits, integers compactly (see
    /// <see cref="CompactIntegerConverter"/>). With the parameter "full", the whole number, for the tooltip: a float as
    /// the shortest text that round-trips, an integer with digit grouping.
    /// </summary>
    public class NodeNumberConverter : IValueConverter
    {
        private static readonly CompactIntegerConverter Compact = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool full = parameter as string == "full";
            return value switch
            {
                double or float or Half => string.Format(culture, full ? "{0:R}" : "{0:G6}", value),
                null => null,
                _ => full ? string.Format(culture, "{0:N0}", value) : Compact.Convert(value, targetType, null, culture),
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
