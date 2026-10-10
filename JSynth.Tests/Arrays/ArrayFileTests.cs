using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Arrays
{
    /// <summary>
    /// Loading arrays from files, typing in sparse entries, and the limit on typed-in values.
    /// </summary>
    public sealed class ArrayFileTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public ArrayFileTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        [Fact]
        public void A_csv_file_is_read_with_parsers_and_its_size_shown()
        {
            ArrayFile file = new() { Path = Write("a.csv", "1.0,2.0,3.0\n4.0,5.0,6.0\n") };

            Assert.Null(file.Error);
            Assert.Equal(ArrayFile.Csv, file.Format);
            Assert.Equal("2×3", file.Summary);
            Assert.Equal("Matrix{Float64}", file.OutputType);
            Assert.Equal($"using Parsers\n_jsynth_read_numbers(raw\"{file.Path}\", ',', Float64)", file.Code);
        }

        [Fact]
        public void A_header_line_is_caught_and_can_be_skipped()
        {
            ArrayFile file = new() { Path = Write("a.csv", "x,y\n1,2\n3,4\n") };
            Assert.Contains("'x' in the first line is not a valid Float64", file.Error);

            file.HasHeader = true;

            Assert.Null(file.Error);
            Assert.Equal("2×2", file.Summary);
            Assert.EndsWith("_jsynth_read_numbers(raw\"" + file.Path + "\", ',', Float64; header = true)", file.Julia);
        }

        [Fact]
        public void A_vector_is_the_values_column_by_column()
        {
            ArrayFile file = new() { Path = Write("a.tsv", "1\t2\n3\t4\n"), Shape = "Vector", ElementType = "Int64" };

            Assert.Equal(ArrayFile.Tsv, file.Format);
            Assert.Equal("4-element", file.Summary);
            Assert.Equal($"vec(_jsynth_read_numbers(raw\"{file.Path}\", '\\t', Int64))", file.Julia);
            Assert.Equal("Vector{Int64}", file.OutputType);
        }

        [Fact]
        public void A_matrix_market_file_makes_a_sparse_matrix()
        {
            ArrayFile file = new()
            {
                Path = Write("a.mtx", "%%MatrixMarket matrix coordinate real general\n% a comment\n1000 2000 5000\n1 1 0.5\n"),
            };

            Assert.Null(file.Error);
            Assert.Equal("SparseMatrixCSC{Float64, Int64}", file.OutputType);
            Assert.False(file.CanPickShape);
            Assert.Equal("1,000×2,000, 0.25% stored", file.Summary);
            Assert.Equal($"SparseMatrixCSC{{Float64, Int64}}(MatrixMarket.mmread(raw\"{file.Path}\"))", file.Julia);
        }

        [Fact]
        public void A_symmetric_matrix_market_file_counts_both_triangles_roughly()
        {
            ArrayFile file = new() { Path = Write("s.mtx", "%%MatrixMarket matrix coordinate real symmetric\n100 100 50\n") };

            Assert.Equal("100×100, ~1% stored", file.Summary);
        }

        [Fact]
        public void Complex_matrix_market_files_are_refused()
        {
            ArrayFile file = new() { Path = Write("c.mtx", "%%MatrixMarket matrix coordinate complex general\n2 2 1\n1 1 1.0 2.0\n") };

            Assert.Equal("Complex values aren't supported.", file.Error);
        }

        [Fact]
        public void A_jld2_array_is_read_by_name_and_converted()
        {
            ArrayFile file = new() { Path = Write("a.jld2", string.Empty), Shape = "SparseVector", NameInFile = "signal" };

            Assert.Null(file.Error);
            Assert.Equal($"convert(SparseVector{{Float64, Int64}}, JLD2.load(raw\"{file.Path}\", raw\"signal\"))", file.Julia);

            file.NameInFile = "";
            Assert.Equal("Name the array as it was saved in the file.", file.Error);
        }

        [Fact]
        public void Missing_files_are_reported_but_relative_paths_are_left_to_Julia()
        {
            ArrayFile file = new() { Path = Path.Combine(_folder, "missing.csv") };
            Assert.Equal("The file doesn't exist.", file.Error);

            file.Path = "data/signal.csv";
            Assert.Null(file.Error);
            Assert.Equal("signal.csv", file.FileName);
        }

        [Fact]
        public void Switching_to_matrix_market_makes_the_shape_sparse()
        {
            ArrayFile file = new() { Path = "a.csv", Shape = "Vector" };

            file.Format = ArrayFile.MatrixMarket;

            Assert.Equal("SparseMatrixCSC", file.Shape);
        }

        [Fact]
        public void A_file_node_is_saved_with_its_format_even_when_it_differs_from_the_extension()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                ArrayFile file = (ArrayFile)MainViewModel.AddNode(nameof(ArrayFile), 30, 60);
                file.Path = "data.txt";
                file.Format = ArrayFile.Tsv;
                file.Shape = "Vector";
                file.HasHeader = true;
                MainViewModel.History.Commit();

                MainViewModel.DuplicateSelection();
                ArrayFile copy = MainViewModel.Nodes.OfType<ArrayFile>().Single(n => n.IsSelected);

                Assert.Equal(file.Julia, copy.Julia);
                Assert.Equal(ArrayFile.Tsv, copy.Format);
            });
        }

        [Fact]
        public void A_sparse_matrix_is_typed_as_entries()
        {
            SparseInput input = new() { Rows = 5, Columns = 4, Text = "1 1 0.5\n5 4 -2" };

            Assert.Null(input.Error);
            Assert.Equal("5×4", input.Size);
            Assert.Equal("2 stored", input.Summary);
            Assert.Equal("SparseMatrixCSC{Float64, Int64}", input.OutputType);
            Assert.Equal("sparse([1, 5], [1, 4], Float64[0.5, -2], 5, 4)", input.Julia);
        }

        [Fact]
        public void A_sparse_vector_is_typed_as_index_and_value()
        {
            SparseInput input = new() { Shape = "SparseVector", ElementType = "Int32", Rows = 10, Text = "3 7; 10 1" };

            Assert.Null(input.Error);
            Assert.Equal("10-element", input.Size);
            Assert.Equal("sparsevec([3, 10], Int32[7, 1], 10)", input.Julia);
        }

        [Fact]
        public void No_entries_makes_an_all_zero_sparse_array()
        {
            SparseInput input = new() { Rows = 3, Columns = 3, Text = "" };

            Assert.Equal("spzeros(Float64, 3, 3)", input.Julia);
        }

        [Theory]
        [InlineData("5 1 1.0", "'5 1 1.0': the row must be from 1 to 4, the column from 1 to 4.")]
        [InlineData("0 1 1.0", "'0 1 1.0': the row must be from 1 to 4, the column from 1 to 4.")]
        [InlineData("1 1", "'1 1' should be a row, a column and a value.")]
        [InlineData("1 1 x", "'x' is not a valid Float64.")]
        [InlineData("1 2 1.0\n1 2 3.0", "(1, 2) is given more than once.")]
        public void Wrong_entries_are_explained(string text, string error)
        {
            SparseInput input = new() { Rows = 4, Columns = 4, Text = text };

            Assert.Equal(error, input.Error);
        }

        [Fact]
        public void Too_many_typed_values_are_refused()
        {
            string values = string.Join(", ", Enumerable.Repeat("1.0", ArrayLiteral.MaxValues + 1));
            VectorNode vector = new() { Text = $"[{values}]" };

            Assert.Equal(ArrayLiteral.TooManyValues, vector.Error);

            vector.Text = new string('1', 1_000_000);
            Assert.Equal(ArrayLiteral.TooManyValues, vector.Error);

            vector.Text = string.Join(", ", Enumerable.Repeat("1.0", ArrayLiteral.MaxValues));
            Assert.Null(vector.Error);
        }
    }
}
