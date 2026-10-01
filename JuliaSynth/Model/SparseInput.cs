using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A small sparse vector or matrix typed in as its size and its stored entries, one per line:
    /// <c>row column value</c> for a matrix, <c>index value</c> for a vector. Everything else is zero.
    /// </summary>
    public class SparseInput : Node
    {
        /// <summary>
        /// One stored value; indices count from 1, as in Julia.
        /// </summary>
        public sealed record Entry(long Row, long Column, string Value);

        private const int PreviewEntries = 3;

        public static IReadOnlyList<string> Shapes { get; } = [JuliaTypes.SparseVector, JuliaTypes.SparseMatrix];

        #region Private Properties
        private string _shape = JuliaTypes.SparseMatrix;
        private string _elementType = "Float64";
        private long _rows = 4;
        private long _columns = 4;
        private string _text = string.Empty;
        private IReadOnlyList<Entry> _entries = [];
        private string? _error;
        #endregion

        #region Public Properties
        [JsonProperty]
        public string Shape
        {
            get => _shape;
            set
            {
                if (value is not null && Shapes.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _shape, value);
                    Update();
                }
            }
        }

        [JsonProperty]
        public string ElementType
        {
            get => _elementType;
            set
            {
                if (value is not null && JuliaTypes.Numeric.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _elementType, value);
                    Update();
                }
            }
        }

        /// <summary>
        /// Number of rows; the length, for a vector.
        /// </summary>
        [JsonProperty]
        public long Rows
        {
            get => _rows;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _rows, Math.Max(0, value));
                Update();
            }
        }

        [JsonProperty]
        public long Columns
        {
            get => _columns;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _columns, Math.Max(0, value));
                Update();
            }
        }

        /// <summary>
        /// The entries as typed; this is what is saved.
        /// </summary>
        [JsonProperty]
        public string Text
        {
            get => _text;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _text, value ?? string.Empty);
                Update();
            }
        }

        public ReadOnlyObservableCollection<string> PossibleTypes { get; } = new([.. JuliaTypes.Numeric]);

        /// <summary>
        /// The parsed entries; empty while <see cref="Error"/> is set.
        /// </summary>
        public IReadOnlyList<Entry> Entries
        {
            get => _entries;
            private set => this.RaiseAndSetIfChanged(ref _entries, value);
        }

        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }

        public bool IsVector => Shape == JuliaTypes.SparseVector;

        public bool IsMatrix => !IsVector;

        public string Symbol => IsVector ? "𝒗" : "𝑨";

        public override string OutputType => JuliaTypes.Compose(Shape, ElementType);

        /// <summary>
        /// "4×4" or "10-element".
        /// </summary>
        public string Size => IsVector ? $"{Rows:N0}-element" : $"{Rows:N0}×{Columns:N0}";

        public string Summary => Error is not null ? "?" : $"{Entries.Count:N0} stored";

        /// <summary>
        /// The first few entries for the node, e.g. "(2, 3) 0.5".
        /// </summary>
        public string Preview => string.Join("\n", Entries.Take(PreviewEntries).Select(Describe))
            + (Entries.Count > PreviewEntries ? "\n⋯" : string.Empty);

        public string Hint => IsVector
            ? "One entry per line: index value, e.g. 3 0.5. Indices count from 1."
            : "One entry per line: row column value, e.g. 2 3 0.5. Indices count from 1.";

        /// <summary>
        /// The value as Julia code, e.g. <c>sparse([1, 2], [1, 3], Float64[0.5, 2.0], 4, 4)</c>.
        /// </summary>
        public string Julia
        {
            get
            {
                if (Error is not null)
                {
                    return Error;
                }

                string values = $"{ElementType}[{string.Join(", ", Entries.Select(e => e.Value))}]";
                string rows = $"[{string.Join(", ", Entries.Select(e => e.Row))}]";
                string columns = $"[{string.Join(", ", Entries.Select(e => e.Column))}]";
                return (IsVector, Entries.Count) switch
                {
                    (true, 0) => $"spzeros({ElementType}, {Rows})",
                    (false, 0) => $"spzeros({ElementType}, {Rows}, {Columns})",
                    (true, _) => $"sparsevec({rows}, {values}, {Rows})",
                    (false, _) => $"sparse({rows}, {columns}, {values}, {Rows}, {Columns})",
                };
            }
        }

        public string Code => Error is null ? $"using SparseArrays\n{Julia}" : Julia;
        #endregion

        public SparseInput()
        {
            Color = NodePalette.Sparse;
            Text = "1 1 1.0\n2 2 2.0\n4 1 -0.5";
        }

        private string Describe(Entry entry)
        {
            return IsVector ? $"({entry.Row}) {entry.Value}" : $"({entry.Row}, {entry.Column}) {entry.Value}";
        }

        private void Update()
        {
            (List<Entry>? entries, string? error) = Parse();
            Entries = entries ?? [];
            Error = error;

            foreach (string name in new[]
            {
                nameof(IsVector), nameof(IsMatrix), nameof(Symbol), nameof(OutputType), nameof(Size), nameof(Summary),
                nameof(Preview), nameof(Hint), nameof(Julia), nameof(Code),
            })
            {
                this.RaisePropertyChanged(name);
            }
        }

        private (List<Entry>?, string?) Parse()
        {
            string[] lines = Text.Replace("\r", string.Empty).Split(['\n', ';']);
            if (lines.Length > ArrayLiteral.MaxValues)
            {
                return (null, ArrayLiteral.TooManyValues);
            }

            int fields = IsVector ? 2 : 3;
            List<Entry> entries = [];
            HashSet<(long, long)> seen = [];
            foreach (string line in lines)
            {
                string[] parts = line.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                if (parts.Length != fields)
                {
                    return (null, $"'{line.Trim()}' should be {(IsVector ? "an index and a value" : "a row, a column and a value")}.");
                }

                long row = Index(parts[0]);
                long column = IsVector ? 1 : Index(parts[1]);
                string value = parts[^1];
                if (row < 1 || row > Rows || column < 1 || (IsMatrix && column > Columns))
                {
                    return (null, IsVector
                        ? $"'{line.Trim()}': the index must be from 1 to {Rows}."
                        : $"'{line.Trim()}': the row must be from 1 to {Rows}, the column from 1 to {Columns}.");
                }

                if (!JuliaTypes.IsValidLiteral(ElementType, value))
                {
                    return (null, $"'{value}' is not a valid {ElementType}.");
                }

                // Julia would add up repeated entries; that is more likely a typo.
                if (!seen.Add((row, column)))
                {
                    return (null, $"{Describe(new Entry(row, column, string.Empty)).Trim()} is given more than once.");
                }

                entries.Add(new Entry(row, column, value));
            }

            return (entries, null);
        }

        private static long Index(string text)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long index) ? index : -1;
        }
    }
}
