using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// Reads vector and matrix values written the Julia way: <c>[1.0, 2.0, 3.0]</c> or <c>[1 2; 3 4]</c>.
    /// Rows are separated by <c>;</c> or new lines, values within a row by commas or spaces; the
    /// brackets are optional, so columns pasted from a spreadsheet work too.
    /// </summary>
    public static class ArrayLiteral
    {
        private static readonly char[] RowSeparators = [';', '\n'];
        private static readonly char[] ValueSeparators = [',', ' ', '\t'];

        /// <summary>
        /// Values typed into a node are saved in the design and parsed on every change; bigger arrays
        /// belong in an array builder or a file.
        /// </summary>
        public const int MaxValues = 10_000;

        // Longer text surely holds too many values; don't even split it.
        private const int MaxLength = MaxValues * 32;

        public const string TooManyValues = "More than 10,000 values is too many to type in: use an array builder or load the values from a file.";

        /// <summary>
        /// The values row by row, or an error saying what is wrong.
        /// </summary>
        public static (List<List<string>>? Rows, string? Error) Parse(string? text, string elementType)
        {
            if (text?.Length > MaxLength)
            {
                return (null, TooManyValues);
            }

            string body = (text ?? string.Empty).Replace("\r", string.Empty).Trim();
            if (body.StartsWith('[') != body.EndsWith(']'))
            {
                return (null, "Brackets don't match.");
            }

            body = body.TrimStart('[').TrimEnd(']');

            List<List<string>> rows = body
                .Split(RowSeparators)
                .Select(row => row.Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries).ToList())
                .Where(row => row.Count > 0)
                .ToList();

            if (rows.Sum(row => row.Count) > MaxValues)
            {
                return (null, TooManyValues);
            }

            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].Count != rows[0].Count)
                {
                    return (null, $"Row {r + 1} has {rows[r].Count} values, row 1 has {rows[0].Count}.");
                }
            }

            string? invalid = rows.SelectMany(row => row).FirstOrDefault(value => !JuliaTypes.IsValidLiteral(elementType, value));
            return invalid is null
                ? (rows, null)
                : (null, $"'{invalid}' is not a valid {elementType}.");
        }

        /// <summary>
        /// The values as Julia code with an explicit element type, e.g. <c>Float64[1.0 2.0; 3.0 4.0]</c>.
        /// </summary>
        public static string ToJulia(IReadOnlyList<IReadOnlyList<string>> rows, string elementType, bool isVector)
        {
            string separator = isVector ? ", " : "; ";
            return isVector
                ? $"{elementType}[{string.Join(separator, rows.SelectMany(row => row))}]"
                : $"{elementType}[{string.Join(separator, rows.Select(row => string.Join(' ', row)))}]";
        }
    }
}
