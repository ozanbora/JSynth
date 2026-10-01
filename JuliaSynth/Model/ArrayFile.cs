using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A vector or matrix read from a data file when the Julia code runs, so the design only holds the path.
    /// The file is looked at here only to show its size and catch mistakes early.
    /// </summary>
    public class ArrayFile : Node
    {
        public const string Csv = "CSV";
        public const string Tsv = "TSV";
        public const string MatrixMarket = "Matrix Market";
        public const string Jld2 = "JLD2";

        public static IReadOnlyList<string> Formats { get; } = [Csv, Tsv, MatrixMarket, Jld2];

        // Delimited files larger than this aren't read through just to count their rows.
        private const long MaxCountedBytes = 16 * 1024 * 1024;

        #region Private Properties
        private string _path = string.Empty;
        private string _format = Csv;
        private string _shape = JuliaTypes.Matrix;
        private string _elementType = "Float64";
        private bool _hasHeader;
        private string _variable = "A";
        private string _summary = string.Empty;
        private string? _error;
        #endregion

        #region Public Properties
        /// <summary>
        /// Where the file is, as Julia will be given it.
        /// </summary>
        // Saved and read back in order: setting the path picks a format, and the format limits the shapes.
        [JsonProperty(Order = 1)]
        public string Path
        {
            get => _path;
            set
            {
                string path = (value ?? string.Empty).Trim().Trim('"');
                if (path == _path)
                {
                    return;
                }

                _path = path;
                this.RaisePropertyChanged();

                // Pick the format from the extension; it can still be changed.
                if (FormatOf(path) is string format)
                {
                    _format = format;
                }

                Update();
            }
        }

        [JsonProperty(Order = 2)]
        public string Format
        {
            get => _format;
            set
            {
                if (value is not null && Formats.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _format, value);
                    Update();
                }
            }
        }

        /// <summary>
        /// Vector, Matrix, SparseMatrixCSC, ...: which ones depends on the format.
        /// </summary>
        [JsonProperty(Order = 3)]
        public string Shape
        {
            get => _shape;
            set
            {
                if (value is not null && JuliaTypes.Shapes.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _shape, value);
                    Update();
                }
            }
        }

        [JsonProperty(Order = 4)]
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
        /// Whether the first line of a CSV or TSV file holds column names, to be skipped.
        /// </summary>
        [JsonProperty(Order = 5)]
        public bool HasHeader
        {
            get => _hasHeader;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _hasHeader, value);
                Update();
            }
        }

        /// <summary>
        /// The name the array was saved under in a JLD2 file.
        /// </summary>
        [JsonProperty(Order = 6)]
        public string Variable
        {
            get => _variable;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _variable, (value ?? string.Empty).Trim());
                Update();
            }
        }

        public IReadOnlyList<string> PossibleShapes => ShapesFor(Format);

        public bool CanPickShape => PossibleShapes.Count > 1;

        public bool IsDelimited => Format is Csv or Tsv;

        public bool IsJld2 => Format == Jld2;

        public bool IsVector => JuliaTypes.Dimensions(Shape) == 1;

        /// <summary>
        /// The math symbol shown on the node: bold v for a vector, bold A for a matrix.
        /// </summary>
        public string Symbol => IsVector ? "𝒗" : "𝑨";

        public override string OutputType => JuliaTypes.Compose(Shape, ElementType);

        public string FileName => _path.Length == 0 ? "no file" : System.IO.Path.GetFileName(_path);

        /// <summary>
        /// What the file holds as far as can be told without reading it all, e.g. "1000×1000, 0.5% stored".
        /// </summary>
        public string Summary
        {
            get => _summary;
            private set => this.RaiseAndSetIfChanged(ref _summary, value);
        }

        /// <summary>
        /// The Julia expression reading the file, e.g. <c>readdlm(raw"C:\data\a.csv", ',', Float64)</c>.
        /// </summary>
        public string Julia
        {
            get
            {
                string path = PathLiteral(_path);
                return Format switch
                {
                    Csv or Tsv => Dense($"readdlm({path}, {(Format == Csv ? "','" : "'\\t'")}, {ElementType}{(HasHeader ? "; skipstart = 1" : string.Empty)})"),
                    MatrixMarket => $"{OutputType}(MatrixMarket.mmread({path}))",
                    _ => $"convert({OutputType}, JLD2.load({path}, {PathLiteral(Variable)}))",
                };
            }
        }

        /// <summary>
        /// <see cref="Julia"/>, after the <c>using</c> it needs.
        /// </summary>
        public string Code => $"using {Package}\n{Julia}";

        public string Package => Format switch
        {
            Csv or Tsv => "DelimitedFiles",
            MatrixMarket => "MatrixMarket, SparseArrays",
            _ => "JLD2, SparseArrays",
        };

        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }
        #endregion

        public ArrayFile()
        {
            Color = NodePalette.ArrayFile;
            Update();
        }

        public static IReadOnlyList<string> ShapesFor(string format) => format switch
        {
            Csv or Tsv => [JuliaTypes.Vector, JuliaTypes.Matrix],
            MatrixMarket => [JuliaTypes.SparseMatrix],
            _ => [JuliaTypes.Vector, JuliaTypes.Matrix, JuliaTypes.SparseVector, JuliaTypes.SparseMatrix],
        };

        /// <summary>
        /// The format a file extension stands for, or null.
        /// </summary>
        public static string? FormatOf(string path)
        {
            return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".csv" => Csv,
                ".tsv" or ".tab" => Tsv,
                ".mtx" => MatrixMarket,
                ".jld2" => Jld2,
                _ => null,
            };
        }

        /// <summary>
        /// A Julia string with the text as is: raw strings leave backslashes (Windows paths) alone.
        /// </summary>
        internal static string PathLiteral(string text)
        {
            return $"raw\"{text.Replace("\"", "\\\"")}\"";
        }

        // readdlm always makes a matrix; vec makes a vector of it, column by column.
        private string Dense(string read) => IsVector ? $"vec({read})" : read;

        private void Update()
        {
            if (!PossibleShapes.Contains(_shape))
            {
                _shape = PossibleShapes.Contains(JuliaTypes.Matrix) ? JuliaTypes.Matrix : PossibleShapes[0];
            }

            (Summary, Error) = Inspect();

            foreach (string name in new[]
            {
                nameof(Format), nameof(Shape), nameof(PossibleShapes), nameof(CanPickShape), nameof(IsDelimited),
                nameof(IsJld2), nameof(IsVector), nameof(Symbol), nameof(OutputType), nameof(FileName), nameof(Julia),
                nameof(Code), nameof(Package),
            })
            {
                this.RaisePropertyChanged(name);
            }
        }

        private (string Summary, string? Error) Inspect()
        {
            if (_path.Length == 0)
            {
                return (string.Empty, "Pick a file.");
            }

            if (IsJld2 && !IsIdentifier(Variable))
            {
                return (string.Empty, "Name the array as it was saved in the file.");
            }

            // A relative path is found from wherever Julia runs, so it can't be checked here.
            if (!System.IO.Path.IsPathFullyQualified(_path))
            {
                return (string.Empty, null);
            }

            if (!File.Exists(_path))
            {
                return (string.Empty, "The file doesn't exist.");
            }

            try
            {
                return Format switch
                {
                    Csv or Tsv => InspectDelimited(),
                    MatrixMarket => InspectMatrixMarket(),
                    _ => (string.Empty, null),
                };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return (string.Empty, $"The file can't be read: {e.Message}");
            }
        }

        private (string, string?) InspectDelimited()
        {
            char separator = Format == Csv ? ',' : '\t';
            using StreamReader reader = new(_path);
            if (HasHeader)
            {
                _ = reader.ReadLine();
            }

            if (reader.ReadLine() is not string first)
            {
                return ("empty", "The file holds no values.");
            }

            string[] values = first.Split(separator).Select(v => v.Trim()).ToArray();
            if (values.FirstOrDefault(v => !JuliaTypes.IsValidLiteral(ElementType, v)) is string invalid)
            {
                return (string.Empty, HasHeader
                    ? $"'{invalid}' in the first row is not a valid {ElementType}."
                    : $"'{invalid}' in the first line is not a valid {ElementType}. If that line names the columns, tick 'First line is a header'.");
            }

            if (new FileInfo(_path).Length > MaxCountedBytes)
            {
                return ($"{values.Length} columns", null);
            }

            int rows = 1;
            while (reader.ReadLine() is string line)
            {
                if (line.Trim().Length > 0)
                {
                    rows++;
                }
            }

            return (IsVector ? $"{rows * values.Length:N0}-element" : $"{rows:N0}×{values.Length:N0}", null);
        }

        private (string, string?) InspectMatrixMarket()
        {
            using StreamReader reader = new(_path);

            // %%MatrixMarket matrix coordinate real general
            string[] banner = (reader.ReadLine() ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (banner.Length < 4 || banner[0] != "%%matrixmarket" || banner[1] != "matrix")
            {
                return (string.Empty, "This is not a Matrix Market matrix file.");
            }

            if (banner[3] == "complex")
            {
                return (string.Empty, "Complex values aren't supported.");
            }

            string? line;
            do
            {
                line = reader.ReadLine();
            }
            while (line is not null && (line.StartsWith('%') || line.Trim().Length == 0));

            long[] size = (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out long n) ? n : -1)
                .ToArray();
            bool coordinate = banner[2] == "coordinate";
            if (size.Length != (coordinate ? 3 : 2) || size.Any(n => n < 0))
            {
                return (string.Empty, "The size line of the file can't be read.");
            }

            string shape = $"{size[0]:N0}×{size[1]:N0}";
            if (!coordinate)
            {
                return ($"{shape}, dense", null);
            }

            // A symmetric (or skew, hermitian) file holds one triangle; the other mirrors it, bar the diagonal.
            bool mirrored = banner.Length > 4 && banner[4] != "general";
            double cells = (double)size[0] * size[1];
            double stored = mirrored ? 2 * size[2] : size[2];
            return (cells == 0 ? shape : $"{shape}, {(mirrored ? "~" : string.Empty)}{stored / cells:0.###%} stored", null);
        }

        private static bool IsIdentifier(string name)
        {
            return name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '!');
        }
    }
}
