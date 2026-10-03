using System.Collections.Generic;

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

        public static IReadOnlyList<string> All { get; } = [Csv, Tsv, MatrixMarket, Jld2];

        public static bool IsDelimited(string format) => format is Csv or Tsv;

        /// <summary>
        /// The shapes a file of <paramref name="format"/> can be read as.
        /// </summary>
        public static IReadOnlyList<string> ShapesFor(string format) => format switch
        {
            Csv or Tsv => [JuliaTypes.Vector, JuliaTypes.Matrix],
            MatrixMarket => [JuliaTypes.SparseMatrix],
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
            _ => "*.jld2",
        };

        /// <summary>
        /// The packages reading <paramref name="format"/>, as for a using statement.
        /// </summary>
        public static string Package(string format) => format switch
        {
            Csv or Tsv => "DelimitedFiles",
            MatrixMarket => "MatrixMarket, SparseArrays",
            _ => "JLD2, SparseArrays",
        };

        /// <summary>
        /// Julia code reading the file at <paramref name="path"/> (a Julia expression: a string literal,
        /// or a variable) as <paramref name="shape"/> of <paramref name="elementType"/>.
        /// </summary>
        /// <param name="hasHeader">A CSV or TSV file's first line names the columns.</param>
        /// <param name="variable">The name the array was saved under in a JLD2 file.</param>
        public static string Read(string format, string path, string shape, string elementType, bool hasHeader, string variable)
        {
            string type = JuliaTypes.Compose(shape, elementType);
            return format switch
            {
                // readdlm always makes a matrix; vec makes a vector of it, column by column.
                Csv or Tsv => Dense(shape, $"readdlm({path}, {(format == Csv ? "','" : "'\\t'")}, {elementType}{(hasHeader ? "; skipstart = 1" : string.Empty)})"),
                MatrixMarket => $"{type}(MatrixMarket.mmread({path}))",
                _ => $"convert({type}, JLD2.load({path}, {ArrayFile.PathLiteral(variable)}))",
            };
        }

        private static string Dense(string shape, string read) => JuliaTypes.Dimensions(shape) == 1 ? $"vec({read})" : read;
    }
}
