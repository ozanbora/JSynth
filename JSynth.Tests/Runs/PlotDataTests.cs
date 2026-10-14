using JSynth.Runtime;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JSynth.Tests.Runs
{
    /// <summary>
    /// Plot data going from Julia to the app. Needs Julia; without it there is nothing to check.
    /// </summary>
    public sealed class PlotDataTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public PlotDataTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private string PathOf(string name) => Path.Combine(_folder, name);

        /// <summary>
        /// Runs <paramref name="code"/> after the plot helpers; returns what it printed to standard error.
        /// </summary>
        private async Task<string?> RunAsync(string code)
        {
            if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
            {
                return null;
            }

            JuliaRunner runner = new(julia.Path);
            ConcurrentQueue<string> errors = new();
            runner.Output += line =>
            {
                if (line.IsError)
                {
                    errors.Enqueue(line.Text);
                }
            };

            RunResult result = await runner.RunAsync($"using SparseArrays\n{PlotData.Prelude}\n{code}", _folder);
            string errorText = string.Join("\n", errors);
            Assert.True(result.Succeeded, errorText);
            return errorText;
        }

        [Fact]
        public async Task Vectors_become_lines_and_points()
        {
            if (await RunAsync("""
                _jsynth_plot("line.jsplot", "auto", [3.0, 1.0, 2.0])
                _jsynth_plot("scatter.jsplot", "scatter", Int32[1, 4, 9], [0.5, 1.0, 1.5])
                """) is null)
            {
                return;
            }

            PlotData line = PlotData.Read(PathOf("line.jsplot"));
            Assert.Equal(PlotKind.Line, line.Kind);
            Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, line.X);
            Assert.Equal(new double[] { 3.0, 1.0, 2.0 }, line.Y);
            Assert.Equal((3L, 1L), (line.Rows, line.Columns));

            PlotData scatter = PlotData.Read(PathOf("scatter.jsplot"));
            Assert.Equal(PlotKind.Scatter, scatter.Kind);
            Assert.Equal(new double[] { 0.5, 1.0, 1.5 }, scatter.X);
            Assert.Equal(new double[] { 1.0, 4.0, 9.0 }, scatter.Y);
        }

        [Fact]
        public async Task A_vector_becomes_bars_at_its_x_values_or_its_indices()
        {
            if (await RunAsync("""
                _jsynth_plot("bars.jsplot", "bar", [3, -1, 2])
                _jsynth_plot("placed.jsplot", "bar", [5.0, 7.0], [1990, 2000])
                """) is null)
            {
                return;
            }

            PlotData bars = PlotData.Read(PathOf("bars.jsplot"));
            Assert.Equal(PlotKind.Bar, bars.Kind);
            Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, bars.X);
            Assert.Equal(new double[] { 3.0, -1.0, 2.0 }, bars.Y);

            PlotData placed = PlotData.Read(PathOf("placed.jsplot"));
            Assert.Equal(new double[] { 1990.0, 2000.0 }, placed.X);
            Assert.Equal(new double[] { 5.0, 7.0 }, placed.Y);
        }

        [Fact]
        public async Task A_histogram_counts_every_value_into_equal_bins_the_largest_in_the_last()
        {
            if (await RunAsync("""
                _jsynth_plot("set.jsplot", "histogram", [0.0, 1.0, 1.5, 2.0, 3.9, 4.0, NaN]; bins = 4)
                _jsynth_plot("auto.jsplot", "histogram", collect(1:100))
                _jsynth_plot("empty.jsplot", "histogram", Float64[])
                _jsynth_plot("same.jsplot", "histogram", fill(7, 5))
                """) is null)
            {
                return;
            }

            // NaN isn't counted; 1.0 starts the second bin, 4.0 (the largest) counts in the last.
            PlotData set = PlotData.Read(PathOf("set.jsplot"));
            Assert.Equal(PlotKind.Histogram, set.Kind);
            Assert.Equal(new double[] { 0.0, 1.0, 2.0, 3.0, 4.0 }, set.X);
            Assert.Equal(new double[] { 1.0, 2.0, 1.0, 2.0 }, set.Y);
            Assert.Equal((7L, 1L), (set.Rows, set.Columns));

            // Auto: √100 = 10 bins, every value counted.
            PlotData auto = PlotData.Read(PathOf("auto.jsplot"));
            Assert.Equal(10, auto.Y.Length);
            Assert.Equal(100.0, auto.Y.Sum());
            Assert.Equal((1.0, 100.0), (auto.X[0], auto.X[^1]));

            // Nothing or one value repeated: one bin.
            PlotData empty = PlotData.Read(PathOf("empty.jsplot"));
            Assert.Equal(new double[] { 0.0, 1.0 }, empty.X);
            Assert.Equal(new double[] { 0.0 }, empty.Y);
            PlotData same = PlotData.Read(PathOf("same.jsplot"));
            Assert.Equal(new double[] { 6.5, 7.5 }, same.X);
            Assert.Equal(new double[] { 5.0 }, same.Y);
        }

        [Fact]
        public async Task Matrices_become_heatmaps_averaged_down_when_big()
        {
            if (await RunAsync("""
                _jsynth_plot("small.jsplot", "auto", [1.0 2.0 3.0; 4.0 5.0 6.0])
                _jsynth_plot("big.jsplot", "auto", ones(2500, 30))
                """) is null)
            {
                return;
            }

            PlotData small = PlotData.Read(PathOf("small.jsplot"));
            Assert.Equal(PlotKind.Heatmap, small.Kind);
            Assert.Equal(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } }, small.Values);

            // 2500 rows need blocks of 3 to fit 1000: 834 × 10.
            PlotData big = PlotData.Read(PathOf("big.jsplot"));
            Assert.Equal((834, 10), (big.Values!.GetLength(0), big.Values.GetLength(1)));
            Assert.Equal((2500L, 30L), (big.Rows, big.Columns));
            Assert.All(big.Values.Cast<double>(), v => Assert.Equal(1.0, v));
        }

        [Fact]
        public async Task Contour_lines_take_a_matrix_written_as_a_heatmaps_is()
        {
            if (await RunAsync("""
                _jsynth_plot("contour.jsplot", "contour", [1.0 2.0 3.0; 4.0 5.0 6.0])
                _jsynth_plot("big.jsplot", "contour", [i + j for i in 1:1500, j in 1:20])
                """) is null)
            {
                return;
            }

            PlotData contour = PlotData.Read(PathOf("contour.jsplot"));
            Assert.Equal(PlotKind.Contour, contour.Kind);
            Assert.Equal(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } }, contour.Values);
            Assert.Equal((2L, 3L), (contour.Rows, contour.Columns));

            // Averaged down as a heatmap is: blocks of 2 for 1500 rows.
            PlotData big = PlotData.Read(PathOf("big.jsplot"));
            Assert.Equal((750, 10), (big.Values!.GetLength(0), big.Values.GetLength(1)));
            Assert.Equal((1500L, 20L), (big.Rows, big.Columns));
        }

        [Fact]
        public async Task Sparse_matrices_become_dots_where_their_values_are()
        {
            if (await RunAsync("""
                _jsynth_plot("sparse.jsplot", "auto", sparse([1, 3], [2, 4], [5.0, 6.0], 3, 4))
                _jsynth_plot("dense.jsplot", "spy", [0 7; 8 0])
                """) is null)
            {
                return;
            }

            PlotData sparse = PlotData.Read(PathOf("sparse.jsplot"));
            Assert.Equal(PlotKind.Spy, sparse.Kind);
            Assert.Equal(new double[] { 2.0, 4.0 }, sparse.X);
            Assert.Equal(new double[] { 1.0, 3.0 }, sparse.Y);
            Assert.Equal((3L, 4L), (sparse.Rows, sparse.Columns));

            PlotData dense = PlotData.Read(PathOf("dense.jsplot"));
            Assert.Equal(new double[] { 1.0, 2.0 }, dense.X.Order());
            Assert.Equal(2, dense.Y.Length);
        }

        [Fact]
        public async Task Plotting_the_wrong_shape_is_an_error_saying_why()
        {
            if (await RunAsync("""
                for (kind, y, x) in (("line", [1 2; 3 4], nothing), ("heatmap", [1.0, 2.0], nothing), ("line", [1.0, 2.0], [1.0]), ("bar", [1 2; 3 4], nothing), ("histogram", [1 2; 3 4], nothing), ("histogram", [1.0, 2.0], [1.0, 2.0]), ("contour", [1.0, 2.0], nothing))
                    try
                        _jsynth_plot("wrong.jsplot", kind, y, x)
                        println(stderr, "no error")
                    catch e
                        println(stderr, sprint(showerror, e))
                    end
                end
                """) is not string errors)
            {
                return;
            }

            Assert.Contains("a line plot needs a vector", errors);
            Assert.Contains("a bar plot needs a vector", errors);
            Assert.Contains("a histogram needs a vector", errors);
            Assert.Contains("a histogram counts the values of y: it takes no x values", errors);
            Assert.Contains("a heatmap needs a matrix", errors);
            Assert.Contains("a contour plot needs a matrix", errors);
            Assert.Contains("1 x values for 2 y values", errors);
            Assert.DoesNotContain("no error", errors);
        }

        [Fact]
        public void Other_files_are_refused()
        {
            File.WriteAllText(PathOf("not.jsplot"), "hello");

            _ = Assert.Throws<InvalidDataException>(() => PlotData.Read(PathOf("not.jsplot")));
        }
    }
}
