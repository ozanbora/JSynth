using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace JuliaSynth.Model
{
    /// <summary>
    /// The Julia types JuliaSynth works with, and whether a literal is a valid value of one.
    /// </summary>
    public static class JuliaTypes
    {
        /// <summary>
        /// Scalar types, usable on their own and as element types of vectors and matrices.
        /// </summary>
        public static IReadOnlyList<string> Numeric { get; } = [
            "Bool",
            "Int8", "Int16", "Int32", "Int64",
            "UInt8", "UInt16", "UInt32", "UInt64",
            "Float16", "Float32", "Float64",
        ];

        public static string VectorOf(string elementType) => $"Vector{{{elementType}}}";

        public static string MatrixOf(string elementType) => $"Matrix{{{elementType}}}";

        /// <summary>
        /// Any, the scalar types, then a vector and a matrix of each.
        /// </summary>
        public static IReadOnlyList<string> All { get; } =
            [Function.AnyType, .. Numeric, .. Numeric.Select(VectorOf), .. Numeric.Select(MatrixOf)];

        /// <summary>
        /// Whether <paramref name="literal"/> is a value of <paramref name="type"/>, e.g. "300" is not an Int8.
        /// </summary>
        public static bool IsValidLiteral(string type, string literal)
        {
            return type switch
            {
                "Bool" => literal is "true" or "false",
                "Float16" or "Float32" or "Float64" => IsFloat(literal),
                _ when IntegerRange(type) is var (min, max) => IsInteger(literal, min, max),
                _ => false,
            };
        }

        private static bool IsFloat(string literal)
        {
            // Julia spells the special values Inf and NaN.
            return literal.TrimStart('-', '+') is "Inf" or "NaN"
                || double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static bool IsInteger(string literal, BigInteger min, BigInteger max)
        {
            string digits = literal.Replace("_", string.Empty);
            return BigInteger.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger value)
                && value >= min && value <= max;
        }

        private static (BigInteger Min, BigInteger Max)? IntegerRange(string type)
        {
            if (!type.StartsWith("Int", StringComparison.Ordinal) && !type.StartsWith("UInt", StringComparison.Ordinal))
            {
                return null;
            }

            bool signed = type[0] == 'I';
            if (!int.TryParse(type.AsSpan(signed ? 3 : 4), out int bits))
            {
                return null;
            }

            return signed
                ? (-(BigInteger.One << (bits - 1)), (BigInteger.One << (bits - 1)) - 1)
                : (BigInteger.Zero, (BigInteger.One << bits) - 1);
        }
    }
}
