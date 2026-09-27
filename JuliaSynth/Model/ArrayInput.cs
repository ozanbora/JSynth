using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A vector or matrix of numbers, entered as Julia-style text (see <see cref="ArrayLiteral"/>).
    /// The text is what is saved, so values stay exactly as typed.
    /// </summary>
    public abstract class ArrayInput : Node
    {
        // Values previewed on the node.
        private const int PreviewRows = 3;
        private const int PreviewColumns = 4;

        private string _elementType = "Float64";
        private string _text = string.Empty;
        private IReadOnlyList<IReadOnlyList<string>> _rows = [];
        private string? _error;

        #region Public Properties
        [JsonProperty]
        public string ElementType
        {
            get => _elementType;
            // Ignore the null a ComboBox writes back when its selection is cleared.
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _elementType, value ?? _elementType);
                Update();
            }
        }

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
        /// The parsed values, row by row; empty while <see cref="Error"/> is set.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Rows
        {
            get => _rows;
            private set => this.RaiseAndSetIfChanged(ref _rows, value);
        }

        /// <summary>
        /// Why <see cref="Text"/> isn't a valid value, or null.
        /// </summary>
        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }

        public abstract bool IsVector { get; }

        /// <summary>
        /// The math symbol shown on the node: bold v for a vector, bold A for a matrix.
        /// </summary>
        public string Symbol => IsVector ? "𝒗" : "𝑨";

        /// <summary>
        /// "3-element" or "2×3"; "?" while the values are invalid.
        /// </summary>
        public string Size => Error is not null ? "?"
            : IsVector ? $"{Rows.Sum(row => row.Count)}-element"
            : $"{Rows.Count}×{Rows[0].Count}";

        public override string OutputType => IsVector ? JuliaTypes.VectorOf(ElementType) : JuliaTypes.MatrixOf(ElementType);

        /// <summary>
        /// The value as Julia code, e.g. <c>Float64[1.0, 2.0]</c>.
        /// </summary>
        public string Julia => Error is null ? ArrayLiteral.ToJulia(Rows, ElementType, IsVector) : Error;

        /// <summary>
        /// The first few rows for the node, with ⋯ and ⋮ where values are left out.
        /// </summary>
        public string Preview
        {
            get
            {
                if (Error is not null)
                {
                    return string.Empty;
                }

                IEnumerable<IReadOnlyList<string>> rows = IsVector ? [Rows.SelectMany(row => row).ToList()] : Rows;
                int columns = IsVector ? 6 : PreviewColumns;
                List<string> lines = rows.Take(PreviewRows)
                    .Select(row => string.Join(IsVector ? ", " : "  ", row.Take(columns)) + (row.Count > columns ? "  ⋯" : string.Empty))
                    .ToList();
                if (!IsVector && Rows.Count > PreviewRows)
                {
                    lines.Add("⋮");
                }

                return string.Join("\n", lines);
            }
        }
        #endregion

        private void Update()
        {
            (List<List<string>>? rows, string? error) = ArrayLiteral.Parse(Text, ElementType);

            // A vector is one row or one column; anything wider belongs in a matrix.
            if (rows is not null && IsVector && rows.Count > 1 && rows[0].Count > 1)
            {
                error = $"A vector has one row or one column, not {rows.Count}×{rows[0].Count}; use a matrix.";
                rows = null;
            }
            else if (rows is not null && !IsVector && rows.Count == 0)
            {
                error = "A matrix needs at least one value.";
                rows = null;
            }

            Rows = rows is null ? [] : rows;
            Error = error;
            this.RaisePropertyChanged(nameof(Size));
            this.RaisePropertyChanged(nameof(Preview));
            this.RaisePropertyChanged(nameof(Julia));
            this.RaisePropertyChanged(nameof(OutputType));
        }
    }

    public class VectorNode : ArrayInput
    {
        public VectorNode()
        {
            Color = Avalonia.Media.Colors.Teal;
            Text = "[1.0, 2.0, 3.0]";
        }

        public override bool IsVector => true;
    }

    public class MatrixNode : ArrayInput
    {
        public MatrixNode()
        {
            Color = Avalonia.Media.Colors.SlateBlue;
            Text = "[1.0 0.0; 0.0 1.0]";
        }

        public override bool IsVector => false;
    }
}
