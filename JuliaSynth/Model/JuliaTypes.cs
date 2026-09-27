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

        public const string Scalar = "Scalar";
        public const string Vector = "Vector";
        public const string Matrix = "Matrix";
        public const string SparseVector = "SparseVector";
        public const string SparseMatrix = "SparseMatrixCSC";
        public const string AbstractVector = "AbstractVector";
        public const string AbstractMatrix = "AbstractMatrix";

        /// <summary>
        /// A type is picked as a shape plus an element type, rather than from one long list. The
        /// abstract shapes take dense and sparse arrays alike, like <c>f(A::AbstractMatrix)</c> in Julia.
        /// </summary>
        public static IReadOnlyList<string> Shapes { get; } = [
            Scalar,
            Vector, SparseVector, AbstractVector,
            Matrix, SparseMatrix, AbstractMatrix,
        ];

        /// <summary>
        /// Any, then the scalar types.
        /// </summary>
        public static IReadOnlyList<string> ElementTypes { get; } = [Function.AnyType, .. Numeric];

        /// <summary>
        /// The floating point types: the only ones some functions (randn, range) make values of.
        /// </summary>
        public static IReadOnlyList<string> Floats { get; } = ["Float16", "Float32", "Float64"];

        // Sparse arrays store their indices as Int64, Julia's default.
        private const string SparseIndexType = "Int64";

        public static string VectorOf(string elementType) => Compose(Vector, elementType);

        public static string MatrixOf(string elementType) => Compose(Matrix, elementType);

        /// <summary>
        /// "Float64", "Vector{Float64}", "SparseMatrixCSC{Float64, Int64}"; an array of Any is Julia's
        /// plain Vector, Matrix, ..., which takes any element type.
        /// </summary>
        public static string Compose(string shape, string elementType)
        {
            return shape == Scalar ? elementType
                : elementType == Function.AnyType ? shape
                : IsSparse(shape) ? $"{shape}{{{elementType}, {SparseIndexType}}}"
                : $"{shape}{{{elementType}}}";
        }

        /// <summary>
        /// The opposite of <see cref="Compose"/>: "Vector{Int64}" is (Vector, Int64), "Matrix" is (Matrix, Any).
        /// </summary>
        public static (string Shape, string ElementType) Split(string type)
        {
            foreach (string shape in Shapes.Skip(1))
            {
                if (type == shape)
                {
                    return (shape, Function.AnyType);
                }

                if (type.StartsWith(shape + "{", StringComparison.Ordinal) && type.EndsWith('}'))
                {
                    // Sparse arrays also name their index type: {Float64, Int64}.
                    return (shape, type[(shape.Length + 1)..^1].Split(',')[0].Trim());
                }
            }

            return (Scalar, type);
        }

        public static bool IsSparse(string shape) => shape is SparseVector or SparseMatrix;

        /// <summary>
        /// 0 for a scalar, 1 for any kind of vector, 2 for any kind of matrix.
        /// </summary>
        public static int Dimensions(string shape) => shape switch
        {
            Vector or SparseVector or AbstractVector => 1,
            Matrix or SparseMatrix or AbstractMatrix => 2,
            _ => 0,
        };

        /// <summary>
        /// Whether a value of <paramref name="shape"/> can be passed where <paramref name="parameterShape"/>
        /// is asked for: the same shape, or any array of the right dimensions for an abstract one.
        /// </summary>
        public static bool FitsShape(string shape, string parameterShape)
        {
            return shape == parameterShape
                || (parameterShape is AbstractVector or AbstractMatrix && Dimensions(shape) == Dimensions(parameterShape));
        }

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
