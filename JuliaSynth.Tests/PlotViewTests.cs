using Avalonia.Controls;
using Avalonia.LogicalTree;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Showing plots: in the node and in a window of their own, on a headless screen.
    /// </summary>
    public sealed class PlotViewTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public PlotViewTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// A plot file as Julia writes it: header, then x and y (or the values of a heatmap).
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
        public void Every_kind_of_plot_shows_in_a_node_and_opens_in_a_window()
        {
            PlotData[] plots =
            [
                Written(PlotKind.Line, 3, 0, 3, 1, [1, 2, 3, 3, 1, 2]),
                Written(PlotKind.Scatter, 2, 0, 2, 1, [0.5, 1.5, 4, 9]),
                Written(PlotKind.Heatmap, 2, 3, 2, 3, [1, 4, 2, 5, 3, 6]),
                Written(PlotKind.Spy, 2, 0, 3, 4, [2, 4, 1, 3]),
            ];

            Assert.Equal(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } }, plots[2].Values);

            Ui.Run(() =>
            {
                foreach (PlotData data in plots)
                {
                    Model.PlotNode model = new() { Title = data.Kind.ToString(), Plot = data };
                    Window host = new() { Content = new Views.Outputs.PlotNode { DataContext = model }, Width = 400, Height = 300 };
                    host.Show();
                    Assert.Null(model.Message);

                    Views.Outputs.PlotWindow window = new(model);
                    window.Show();
                    window.Close();
                    host.Close();
                }
            });
        }

        [Fact]
        public void A_heatmap_drawn_again_and_again_keeps_one_colour_bar_and_where_it_was_zoomed()
        {
            Ui.Run(() =>
            {
                Model.PlotNode model = new() { Title = "live", Plot = Written(PlotKind.Heatmap, 2, 3, 2, 3, [1, 4, 2, 5, 3, 6]) };
                Views.Outputs.PlotWindow window = new(model);
                window.Show();
                window.Chart.Axes.SetLimits(1, 2, 2, 1);
                ScottPlot.Panels.ColorBar bar = window.Chart.Axes.GetPanels().OfType<ScottPlot.Panels.ColorBar>().Single();
                ScottPlot.Plottables.Heatmap heatmap = window.Chart.GetPlottables().OfType<ScottPlot.Plottables.Heatmap>().Single();

                // Live, in a stream: a new drawing of the same size four times a second. Only its numbers change:
                // a colour bar drawn anew would be missing for a moment, and the plot would stretch into its place.
                for (int frame = 0; frame < 5; frame++)
                {
                    model.Plot = Written(PlotKind.Heatmap, 2, 3, 2, 3, [frame, 4, 2, 5, 3, 6]);
                }

                Assert.Same(bar, window.Chart.Axes.GetPanels().OfType<ScottPlot.Panels.ColorBar>().Single());
                Assert.Same(heatmap, window.Chart.GetPlottables().OfType<ScottPlot.Plottables.Heatmap>().Single());
                Assert.Equal(4, heatmap.Intensities[0, 0]);
                ScottPlot.AxisLimits limits = window.Chart.Axes.GetLimits();
                Assert.Equal((1, 2), (limits.Left, limits.Right));

                // Another size is another picture: shown whole.
                model.Plot = Written(PlotKind.Heatmap, 1, 2, 1, 2, [1, 2]);
                Assert.Equal((0.5, 2.5), (window.Chart.Axes.GetLimits().Left, window.Chart.Axes.GetLimits().Right));
                window.Close();
            });
        }

        [Fact]
        public void A_node_without_numbers_says_what_to_do()
        {
            Ui.Run(() =>
            {
                Model.PlotNode model = new();
                Window host = new() { Content = new Views.Outputs.PlotNode { DataContext = model } };
                host.Show();

                TextBlock message = host.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == model.Message);
                Assert.True(message.IsVisible);
                Assert.StartsWith("Run the design", message.Text);
                host.Close();
            });
        }
    }
}
