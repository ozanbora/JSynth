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
        /// The values row by row, or an error saying what is wrong.
        /// </summary>
        public static (List<List<string>>? Rows, string? Error) Parse(string? text, string elementType)
        {
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
