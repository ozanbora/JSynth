using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A vector or matrix read from a data file when the Julia code runs, so the design only holds the path.
    /// The file is looked at here only to show its size and catch mistakes early.
    /// </summary>
    public class ArrayFile : Node
    {
        public const string Csv = FileFormats.Csv;
        public const string Tsv = FileFormats.Tsv;
        public const string MatrixMarket = FileFormats.MatrixMarket;
        public const string Jld2 = FileFormats.Jld2;
        public const string Png = FileFormats.Png;

        public static IReadOnlyList<string> Formats => FileFormats.All;

        // Delimited files larger than this aren't read through just to count their rows.
        private const long MaxCountedBytes = 16 * 1024 * 1024;

        #region Private Properties
        private string _path = string.Empty;
        private string _format = Csv;
        private string _shape = JuliaTypes.Matrix;
        private string _elementType = "Float64";
        private bool _hasHeader;
        private bool _hasComments;
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
        /// Whether lines of a CSV or TSV file starting with # are comments, to be skipped: GeoCSV files describe their
        /// data so, a seismometer's say.
        /// </summary>
        [JsonProperty(Order = 12)]
        public bool HasComments
        {
            get => _hasComments;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _hasComments, value);
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

        public bool IsImage => FileFormats.IsImage(Format);

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
        public string Julia => FileFormats.Read(Format, PathLiteral(_path), Shape, ElementType, HasHeader, Variable, HasComments);

        /// <summary>
        /// <see cref="Julia"/>, after the <c>using</c> it needs.
        /// </summary>
        public string Code => $"using {Package}\n{Julia}";

        public string Package => FileFormats.Package(Format);

        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }
        #endregion

        public ArrayFile()
        {
            Next = new Parameter("next", Function.AnyType);
            Color = NodePalette.ArrayFile;
            Update();
        }

        public static IReadOnlyList<string> ShapesFor(string format) => FileFormats.ShapesFor(format);

        /// <summary>
        /// The format a file's extension stands for, or null.
        /// </summary>
        public static string? FormatOf(string path) => FileFormats.OfExtension(System.IO.Path.GetExtension(path));

        /// <summary>
        /// A Julia string with the text as is: raw strings leave backslashes (Windows paths) alone.
        /// </summary>
        internal static string PathLiteral(string text)
        {
            return $"raw\"{text.Replace("\"", "\\\"")}\"";
        }

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
                nameof(IsJld2), nameof(IsImage), nameof(IsVector), nameof(Symbol), nameof(OutputType), nameof(FileName), nameof(Julia),
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
                    Png => InspectPng(),
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

            // The next line that isn't a comment, when # starts them.
            string? Next()
            {
                string? line;
                do
                {
                    line = reader.ReadLine();
                }
                while (HasComments && line is not null && line.TrimStart().StartsWith('#'));
                return line;
            }

            if (HasHeader)
            {
                _ = Next();
            }

            if (Next() is not string first)
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
            while (Next() is string line)
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

        /// <summary>
        /// Size, bit depth and colours, from the header of a PNG file (its IHDR chunk).
        /// </summary>
        private (string, string?) InspectPng()
        {
            byte[] header = new byte[26];
            using (FileStream stream = File.OpenRead(_path))
            {
                stream.ReadExactly(header, 0, Math.Min(header.Length, (int)stream.Length));
            }

            return PngHeader(header, ElementType);
        }

        /// <summary>
        /// What the first 26 bytes of a PNG file say, for an image read as <paramref name="elementType"/>:
        /// "480×640, 16-bit grey", or why it can't be read so.
        /// </summary>
        internal static (string Summary, string? Error) PngHeader(ReadOnlySpan<byte> header, string elementType)
        {
            ReadOnlySpan<byte> signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
            if (header.Length < 26 || !header[..8].SequenceEqual(signature) || !header[12..16].SequenceEqual("IHDR"u8))
            {
                return (string.Empty, "This is not a PNG image.");
            }

            uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
            uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
            int depth = header[24];
            string colours = header[25] switch
            {
                0 => "grey",
                2 => "colour",
                3 => "palette colour",
                4 => "grey with alpha",
                6 => "colour with alpha",
                _ => "unknown colours",
            };

            // Rows are the image's height: row 1 is its top line.
            string summary = $"{height:N0}×{width:N0}, {depth}-bit {colours}";
            if (JuliaTypes.Floats.Contains(elementType))
            {
                return (summary, null);
            }

            // Integers hold the values stored: up to 255, or 65535 for 16 bits (colour turns into 8 or 16-bit grey).
            BigInteger largest = depth == 16 ? 65535 : 255;
            return JuliaTypes.IntegerRange(elementType) is not (_, var max) || max < largest
                ? (summary, $"Pixels of this image go up to {largest}: pick an integer type holding that, or a float type for intensities from 0 to 1.")
                : (summary, null);
        }

        private static bool IsIdentifier(string name)
        {
            return name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '!');
        }
    }
}
