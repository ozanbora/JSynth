using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using JSynth.Controls;
using JSynth.Runtime;
using JSynth.Views.Outputs;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// What a plot window says about where the mouse points.
    /// </summary>
    public sealed class PlotReadoutTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public PlotReadoutTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// A plot file as Julia writes it: header, then x and y (or the values of a heatmap, column by column).
        /// </summary>
        private PlotData Written(PlotKind kind, long a, long b, long rows, long columns, double[] numbers)
        {
            string path = Path.Combine(_folder, $"{Guid.NewGuid():N}.jsplot");
            using (BinaryWriter writer = new(File.Create(path)))
            {
                writer.Write("JSPLOT01"u8.ToArray());
                foreach (long n in new[] { (long)kind, a, b, rows, columns })
                {
                    writer.Write(n);
                }

                foreach (double x in numbers)
                {
                    writer.Write(x);
                }
            }

            return PlotData.Read(path);
        }

        [Fact]
        public void On_a_line_the_nearest_point_counts_both_axes_by_what_is_shown()
        {
            // (1, 0), (2, 100), (3, 0): y spans 100 where x spans 2.
            PlotReadout readout = new(Written(PlotKind.Line, 3, 0, 3, 1, [1, 2, 3, 0, 100, 0]));

            (string text, (double X, double Y)? point) = readout.Describe(1.9, 10, 2, 100);

            // Near point 2 in x, but far below it once each axis is measured by its span: point 1 or 3 is nearer.
            Assert.Equal((1.0, 0.0), point);
            Assert.Equal("x 1.9   y 10      point 1: (1, 0)", text);
            Assert.Equal(1, readout.Nearest(2.1, 90, 2, 100));
        }

        [Fact]
        public void On_a_bar_chart_the_bar_under_the_mouse_is_given_from_zero_to_its_top()
        {
            // Bars at 1, 2, 3 of heights 3, -1, 2: 0.8 wide.
            PlotReadout readout = new(Written(PlotKind.Bar, 3, 0, 3, 1, [1, 2, 3, 3, -1, 2]));

            (string text, (double X, double Y)? point) = readout.Describe(2.3, -0.5, 4, 5);
            Assert.Equal("x 2.3   y -0.5      bar 2: x 2, value -1", text);
            Assert.Equal((2.0, -1.0), point);

            Assert.Equal(0, readout.Bar(0.7, 2.9));
            Assert.Null(readout.Bar(1.5, 1));   // between bars
            Assert.Null(readout.Bar(3, 2.5));   // above the third bar's top
            Assert.Null(readout.Bar(2, 0.5));   // above the zero line, the second bar going down
        }

        [Fact]
        public void Bars_are_as_wide_as_fits_the_closest_two_with_a_gap_and_drawn_so()
        {
            Assert.Equal(0.4, PlotRenderer.BarWidth([0, 1, 1.5]), 12);
            Assert.Equal(0.8, PlotRenderer.BarWidth([5]), 12);
            Assert.Equal(8, PlotRenderer.BarWidth([1990, 2000, 2020]), 12);

            ScottPlot.Plot plot = new();
            PlotRenderer.Draw(plot, Written(PlotKind.Bar, 3, 0, 3, 1, [0, 1, 1.5, 3, -1, 2]), "bars", ScottPlot.Colors.Black, compact: false);
            ScottPlot.Plottables.BarPlot bars = Assert.Single(plot.GetPlottables().OfType<ScottPlot.Plottables.BarPlot>());
            Assert.Equal([3.0, -1.0, 2.0], bars.Bars.Select(b => b.Value));
            Assert.All(bars.Bars, b => Assert.Equal(0.4, b.Size, 12));
        }

        [Fact]
        public void On_a_histogram_the_bin_under_the_mouse_is_given_with_its_range_and_count()
        {
            // Bins 0–0.5, 0.5–1, 1–1.5 counting 4, 0, 143.
            PlotReadout readout = new(Written(PlotKind.Histogram, 3, 0, 147, 1, [0, 0.5, 1, 1.5, 4, 0, 143]));

            (string text, (double X, double Y)? point) = readout.Describe(1.2, 100, 2, 150);
            Assert.Equal("x 1.2   y 100      1 – 1.5: 143", text);
            Assert.Equal((1.25, 143.0), point);

            Assert.Equal(0, readout.Bin(0.5 - 1e-9, 3));
            Assert.Equal(2, readout.Bin(1.5, 1));      // the largest value's edge is the last bin's
            Assert.Null(readout.Bin(0.7, 0.5));        // over an empty bin
            Assert.Null(readout.Bin(0.2, 5));          // above the first bin's count
            Assert.Null(readout.Bin(1.6, 1));          // past the last edge
        }

        [Fact]
        public void A_histogram_is_drawn_as_bars_each_as_wide_as_its_bin()
        {
            ScottPlot.Plot plot = new();
            PlotRenderer.Draw(plot, Written(PlotKind.Histogram, 2, 0, 3, 1, [0, 2, 3, 1, 2]), "counts", ScottPlot.Colors.Black, compact: false);
            ScottPlot.Plottables.BarPlot bars = Assert.Single(plot.GetPlottables().OfType<ScottPlot.Plottables.BarPlot>());
            Assert.Equal([(1.0, 2.0, 1.0), (2.5, 1.0, 2.0)], bars.Bars.Select(b => (b.Position, b.Size, b.Value)));
        }

        [Fact]
        public void On_a_heatmap_the_row_column_and_value_under_the_mouse_are_given()
        {
            // [1 2 3; 4 5 6], written column by column.
            PlotReadout readout = new(Written(PlotKind.Heatmap, 2, 3, 2, 3, [1, 4, 2, 5, 3, 6]));

            Assert.Equal(("row 2, column 3: 6", null), readout.Describe(3.2, 1.6, 3, 2));
            Assert.Equal(("row 1, column 1: 1", null), readout.Describe(0.6, 1.4, 3, 2));

            // Off the matrix: just where the mouse is.
            Assert.Equal(("x 4   y 1", null), readout.Describe(4, 1, 3, 2));
        }

        [Fact]
        public void A_heatmap_averaged_down_says_its_value_is_an_average()
        {
            // A 4×4 matrix shown as 2×2 blocks.
            PlotReadout readout = new(Written(PlotKind.Heatmap, 2, 2, 4, 4, [1, 2, 3, 4]));

            (string text, _) = readout.Describe(4, 4, 4, 4);

            Assert.Equal("row 4, column 4: ≈ 4   (an average: the matrix is shown averaged down)", text);
        }

        [Fact]
        public void On_a_sparse_matrix_a_stored_entry_is_marked()
        {
            // Dots at (row 1, column 2) and (row 3, column 3): X holds columns, Y rows.
            PlotReadout readout = new(Written(PlotKind.Spy, 2, 0, 3, 3, [2, 3, 1, 3]));

            Assert.Equal(("row 1, column 2: stored", (2.0, 1.0)), readout.Describe(2.1, 0.9, 3, 3));
            Assert.Equal(("row 2, column 2: not stored", null), readout.Describe(2, 2, 3, 3));
        }

        [Theory]
        [InlineData(3, 4, 3, 4)]
        [InlineData(2, 2, 4, 4)]
        public void Heatmap_cells_have_their_edges_halfway_between_Julias_row_and_column_numbers(long shownRows, long shownColumns, long rows, long columns)
        {
            PlotData data = Written(PlotKind.Heatmap, shownRows, shownColumns, rows, columns, new double[shownRows * shownColumns]);
            ScottPlot.Plot plot = new();

            PlotRenderer.Draw(plot, data, "A", ScottPlot.Colors.Black, compact: false);

            ScottPlot.AxisLimits edges = plot.GetPlottables().OfType<ScottPlot.Plottables.Heatmap>().Single().GetAxisLimits();
            Assert.Equal((0.5, columns + 0.5), (Math.Round(edges.Left, 9), Math.Round(edges.Right, 9)));
            Assert.Equal((0.5, rows + 0.5), (Math.Round(Math.Min(edges.Top, edges.Bottom), 9), Math.Round(Math.Max(edges.Top, edges.Bottom), 9)));
        }

        [Theory]
        [InlineData(3.14159265, "3.14159")]
        [InlineData(1234567.0, "1.23457E+06")]
        [InlineData(-0.5, "-0.5")]
        public void Numbers_are_short_and_the_same_in_every_language(double value, string text)
        {
            Assert.Equal(text, PlotReadout.Number(value));
        }

        [Fact]
        public void In_the_window_a_crosshair_follows_the_mouse_and_the_bar_says_where_it_points()
        {
            PlotData data = Written(PlotKind.Line, 3, 0, 3, 1, [1, 2, 3, 1, 4, 9]);
            Ui.Run(() =>
            {
                Model.PlotNode model = new() { Plot = data };
                PlotWindow window = new(model);
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PlotReadout.Hint, window.Readout);
                Assert.False(window.Crosshair!.IsVisible);

                // The middle of the plot: near x = 2.
                window.MouseMove(new Point(window.Bounds.Width / 2, window.Bounds.Height / 2));
                Dispatcher.UIThread.RunJobs();

                // (Axis margins and the bar put the window's middle near, not at, the data's.)
                Assert.True(window.Crosshair!.IsVisible);
                Assert.Matches(@"^x 1\.9\d*   y \d", window.Readout);
                Assert.EndsWith("point 2: (2, 4)", window.Readout);

                // Out over the axes: nothing to point at.
                window.MouseMove(new Point(2, 2));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PlotReadout.Hint, window.Readout);
                window.Close();
            });
        }
    }
}
