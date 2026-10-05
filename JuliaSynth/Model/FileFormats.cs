using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
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
            Csv or Tsv => "DelimitedFiles",
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
                // readdlm always makes a matrix; vec makes a vector of it, column by column.
                // With comments, the header is the first line that isn't one: readdlm then gives the values and it.
                Csv or Tsv => Dense(shape, (hasHeader, hasComments) switch
                {
                    (true, true) => $"readdlm({path}, {Delimiter(format)}, {elementType}; header = true, comments = true)[1]",
                    (true, false) => $"readdlm({path}, {Delimiter(format)}, {elementType}; skipstart = 1)",
                    (false, true) => $"readdlm({path}, {Delimiter(format)}, {elementType}; comments = true)",
                    _ => $"readdlm({path}, {Delimiter(format)}, {elementType})",
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
