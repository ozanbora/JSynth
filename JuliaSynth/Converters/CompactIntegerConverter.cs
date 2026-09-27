using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace JuliaSynth.Converters
{
    /// <summary>
    /// Shows an integer so it fits on a node: with digit grouping when short enough, as a power of
    /// two for the type limits (−2⁶³, 2⁶⁴ − 1), otherwise in exponent form like the float nodes.
    /// </summary>
    public class CompactIntegerConverter : IValueConverter
    {
        // "-2,147,483,648" (the 32-bit limits) still fits; the 64-bit ones don't.
        public const int MaxLength = 14;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            BigInteger? number = value switch
            {
                long l => l,
                ulong u => u,
                int i => i,
                uint u => u,
                Int128 i => i,
                UInt128 u => u,
                BigInteger b => b,
                _ => null,
            };

            return number is BigInteger n ? Format(n, culture) : value?.ToString();
        }

        public static string Format(BigInteger value, CultureInfo culture)
        {
            string grouped = value.ToString("N0", culture);
            if (grouped.Length <= MaxLength)
            {
                return grouped;
            }

            // −2ⁿ and 2ⁿ − 1 are the limits of the n+1-bit signed and n-bit unsigned types.
            if (value < 0 && IsPowerOfTwo(-value, out int bits))
            {
                return "−2" + Superscript(bits);
            }

            if (value > 0 && IsPowerOfTwo(value + 1, out bits))
            {
                return "2" + Superscript(bits) + " − 1";
            }

            return ((double)value).ToString("G6", culture);
        }

        private static bool IsPowerOfTwo(BigInteger value, out int exponent)
        {
            exponent = (int)(value.GetBitLength() - 1);
            return value > 0 && value.IsPowerOfTwo;
        }

        private static string Superscript(int number)
        {
            return string.Concat(number.ToString(CultureInfo.InvariantCulture).Select(digit => "⁰¹²³⁴⁵⁶⁷⁸⁹"[digit - '0']));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
