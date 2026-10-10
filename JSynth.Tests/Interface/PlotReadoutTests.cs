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
