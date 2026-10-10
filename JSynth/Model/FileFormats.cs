using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
{
    /// <summary>
    /// The data file formats arrays are read from, shared by the nodes reading one file
    /// (<see cref="ArrayFile"/>) and every file of a folder (<see cref="FilesNode"/>): their extensions,
    /// the packages reading them, and the Julia code doing it. A new format is added here.
    /// </summary>
    public static class FileFormats
    {
        public const string Csv = "CSV";
        public const string Tsv = "TSV";
        public const string MatrixMarket = "Matrix Market";
        public const string Jld2 = "JLD2";
        public const string Png = "PNG image";
        public const string Json = "JSON";

        public static IReadOnlyList<string> All { get; } = [Csv, Tsv, MatrixMarket, Jld2, Png, Json];

        public static bool IsDelimited(string format) => format is Csv or Tsv;

        public static bool IsImage(string format) => format == Png;

        /// <summary>
        /// Whether a file of <paramref name="format"/> holds a structure (JSON: objects, lists, numbers and text) rather
        /// than an array: it has no shape or element type to pick, and its value is whatever it holds.
        /// </summary>
        public static bool IsStructured(string format) => format == Json;

        /// <summary>
        /// The shapes a file of <paramref name="format"/> can be read as.
        /// </summary>
        public static IReadOnlyList<string> ShapesFor(string format) => format switch
        {
            Csv or Tsv => [JuliaTypes.Vector, JuliaTypes.Matrix],
            MatrixMarket => [JuliaTypes.SparseMatrix],
            Png => [JuliaTypes.Matrix],
            Json => [JuliaTypes.Scalar],
            _ => [JuliaTypes.Vector, JuliaTypes.Matrix, JuliaTypes.SparseVector, JuliaTypes.SparseMatrix],
        };

        /// <summary>
        /// The format a file extension (".csv", with the dot) stands for, or null.
        /// </summary>
        public static string? OfExtension(string extension) => extension.ToLowerInvariant() switch
        {
            ".csv" => Csv,
            ".tsv" or ".tab" => Tsv,
            ".mtx" => MatrixMarket,
            ".jld2" => Jld2,
            ".png" => Png,
            ".json" => Json,
            _ => null,
        };

        /// <summary>
        /// The usual file name pattern of <paramref name="format"/>, e.g. <c>*.csv</c>.
        /// </summary>
        public static string Pattern(string format) => format switch
        {
            Csv => "*.csv",
            Tsv => "*.tsv",
            MatrixMarket => "*.mtx",
            Png => "*.png",
            Json => "*.json",
            _ => "*.jld2",
        };

        /// <summary>
        /// The packages reading <paramref name="format"/>, as for a using statement.
        /// </summary>
        public static string Package(string format) => format switch
        {
            Csv or Tsv => "Parsers",
            MatrixMarket => "MatrixMarket, SparseArrays",
            Png => "PNGFiles",
            Json => "JSON",
            _ => "JLD2, SparseArrays",
        };

        /// <summary>
        /// Packages <paramref name="format"/> is read with that are loaded with import rather than using, so
        /// their many names (ColorTypes: red, gray, alpha, ...) don't clash with the design's own.
        /// </summary>
        public static IReadOnlyList<string> Imports(string format) => format == Png ? ["ColorTypes"] : [];

        /// <summary>
        /// A folder of data files as this system writes one, for an empty folder box to suggest.
        /// </summary>
        public static string ExampleFolder { get; } = OperatingSystem.IsWindows() ? @"C:\data\runs" : "/home/me/data/runs";

        /// <summary>
        /// A data file as this system writes one, for an empty file box to suggest.
        /// </summary>
        public static string ExampleFile { get; } = OperatingSystem.IsWindows() ? @"C:\data\signal.csv" : "/home/me/data/signal.csv";

        /// <summary>
        /// Julia code reading a CSV or TSV file of numbers, for runs and exported scripts alike: with Parsers rather
        /// than readdlm, which is many times faster, and keeps being so on several threads (on Windows readdlm's
        /// numbers take a lock, and get slower the more threads read at once).
        /// </summary>
        public static string Prelude { get; } = """
            # Numbers separated by delim, a row a line, as a matrix of T; made by JSynth. header: the first line (not a
            # comment) names the columns. comments: what follows a # on a line is left out.
            function _jsynth_read_numbers(path, delim::Char, ::Type{T}; header = false, comments = false) where {T}
                values = T[]
                columns, rows = 0, 0
                for (k, line) in enumerate(eachsplit(read(path, String), '\n'))
                    text = strip(comments ? first(split(line, '#'; limit = 2)) : line)
                    isempty(text) && continue
                    header && (header = false; continue)
                    n = 0
                    for field in eachsplit(text, delim)
                        x = Parsers.tryparse(T, strip(field))
                        x === nothing && throw(ArgumentError("line $k of $(basename(path)): $(repr(strip(field))) can't be read as $T"))
                        push!(values, x)
                        n += 1
                    end
                    rows > 0 && n != columns && throw(ArgumentError(string("line $k of $(basename(path)) has ", n, n == 1 ? " value" : " values", "; the lines before it have $columns")))
                    columns = n
                    rows += 1
                end
                permutedims(reshape(values, columns, rows))
            end
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// The Julia character between the values of a CSV or TSV file.
        /// </summary>
        private static string Delimiter(string format) => format == Csv ? "','" : "'\\t'";

        /// <summary>
        /// Julia code reading the file at <paramref name="path"/> (a Julia expression: a string literal,
        /// or a variable) as <paramref name="shape"/> of <paramref name="elementType"/>.
        /// </summary>
        /// <param name="hasHeader">A CSV or TSV file's first line names the columns.</param>
        /// <param name="variable">The name the array was saved under in a JLD2 file.</param>
        /// <param name="hasComments">A CSV or TSV file's lines starting with # are comments (GeoCSV's, say), skipped.</param>
        public static string Read(string format, string path, string shape, string elementType, bool hasHeader, string variable, bool hasComments = false)
        {
            string type = JuliaTypes.Compose(shape, elementType);
            return format switch
            {
                // Always a matrix; vec makes a vector of it, column by column.
                Csv or Tsv => Dense(shape, (hasHeader, hasComments) switch
                {
                    (true, true) => $"_jsynth_read_numbers({path}, {Delimiter(format)}, {elementType}; header = true, comments = true)",
                    (true, false) => $"_jsynth_read_numbers({path}, {Delimiter(format)}, {elementType}; header = true)",
                    (false, true) => $"_jsynth_read_numbers({path}, {Delimiter(format)}, {elementType}; comments = true)",
                    _ => $"_jsynth_read_numbers({path}, {Delimiter(format)}, {elementType})",
                }),
                MatrixMarket => $"{type}(MatrixMarket.mmread({path}))",

                // Grey levels (colour turned to grey by luminance), row 1 at the top: as intensities from 0 to 1
                // for a float type, else as the values stored (0 to 255, or 65535 for a 16-bit image).
                Png when JuliaTypes.Floats.Contains(elementType) => $"{type}(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load({path}))))",
                Png => $"{type}(reinterpret.(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load({path})))))",
                Json => $"JSON.parsefile({path})",
                _ => $"convert({type}, JLD2.load({path}, {ArrayFile.PathLiteral(variable)}))",
            };
        }

        private static string Dense(string shape, string read) => JuliaTypes.Dimensions(shape) == 1 ? $"vec({read})" : read;
    }
}
