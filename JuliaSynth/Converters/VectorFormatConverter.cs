using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace JuliaSynth.Converters
{
    public class VectorFormatConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value.GetType() != typeof(Vector))
            {
                throw new ArgumentException($"Expected argument of type {typeof(Vector)}, but got {value.GetType()}.");
            }

            Vector vector = (Vector)value;

            static string format(double d)
            {
                return string.Format("{0:0.##}", d);
            }

            return $"({format(vector.X)}, {format(vector.Y)})";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
