using JSynth.Runtime;
using ScottPlot;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Controls;

/// <summary>
/// Draws <see cref="PlotData"/> with ScottPlot, in the colours of where it is shown.
/// </summary>
public static class PlotRenderer
{
    /// <summary>
    /// Replaces what <paramref name="plot"/> shows with <paramref name="data"/>; empty for null.
    /// </summary>
    /// <param name="foreground">Colour of text, axes and lines: readable on the background.</param>
    /// <param name="compact">Small labels, for a plot inside a node.</param>
    /// <param name="levels">Contour lines: how many levels, 0 for about 10 round ones (see <see cref="ContourLines.Levels"/>).</param>
    public static void Draw(Plot plot, PlotData? data, string title, Color foreground, bool compact, long levels = 0)
    {
        plot.Clear();

        // A heatmap's colour bar is a panel beside the plot, which Clear leaves: drawn again and again (live,
        // in a stream), they would pile up.
        foreach (ScottPlot.Panels.ColorBar bar in plot.Axes.GetPanels().OfType<ScottPlot.Panels.ColorBar>().ToList())
        {
            plot.Axes.Remove(bar);
        }

        plot.Axes.Title.Label.Text = title;
        Style(plot, foreground, compact);

        // Rules of the last drawing (square cells) don't carry over.
        plot.Axes.Rules.Clear();
        if (data is null)
        {
            return;
        }

        switch (data.Kind)
        {
            case PlotKind.Line:
                ScottPlot.Plottables.Scatter line = plot.Add.ScatterLine(data.X, data.Y);
                line.LineWidth = compact ? 1.5f : 2;
                break;

            case PlotKind.Scatter:
                ScottPlot.Plottables.Scatter points = plot.Add.ScatterPoints(data.X, data.Y);
                points.MarkerSize = MarkerSize(data.X.Length, compact);
                break;

            case PlotKind.Bar:
                ScottPlot.Plottables.BarPlot bars = plot.Add.Bars(data.X, data.Y);
                double width = BarWidth(data.X);
                foreach (ScottPlot.Bar bar in bars.Bars)
                {
                    bar.Size = width;
                    bar.LineWidth = 0;
                }

                break;

            case PlotKind.Histogram:
                // Each bin's bar spans its edges, no gaps between.
                double[] centres = [.. data.X.Zip(data.X.Skip(1), (a, b) => (a + b) / 2)];
                ScottPlot.Plottables.BarPlot bins = plot.Add.Bars(centres, data.Y);
                for (int i = 0; i < bins.Bars.Count; i++)
                {
                    bins.Bars[i].Size = data.X[i + 1] - data.X[i];
                    bins.Bars[i].LineWidth = 0;
                }

                break;

            case PlotKind.Heatmap:
                ScottPlot.Plottables.Heatmap heatmap = plot.Add.Heatmap(data.Values!);
                heatmap.Colormap = new ScottPlot.Colormaps.Viridis();

                // Rows and columns numbered as in Julia, row 1 at the top, even when averaged down: cell
                // edges from 0.5 to the size + 0.5. ScottPlot centres the corner cells on the extent's
                // corners, so the extent runs half a (shown) cell inside those edges.
                int shownRows = data.Values!.GetLength(0), shownColumns = data.Values.GetLength(1);
                double halfWidth = shownColumns > 1 ? data.Columns / (2.0 * shownColumns) : 0;
                double halfHeight = shownRows > 1 ? data.Rows / (2.0 * shownRows) : 0;
                heatmap.Extent = new CoordinateRect(0.5 + halfWidth, data.Columns + 0.5 - halfWidth, data.Rows + 0.5 - halfHeight, 0.5 + halfHeight);
                if (!compact)
                {
                    _ = plot.Add.ColorBar(heatmap);
                }

                break;

            case PlotKind.Contour:
                Contours(plot, data, levels, foreground, compact);
                break;

            case PlotKind.Spy:
                ScottPlot.Plottables.Scatter dots = plot.Add.ScatterPoints(data.X, data.Y);
                dots.MarkerSize = MarkerSize(data.X.Length, compact) - 1;
                break;
        }

        plot.Axes.AutoScale();

        // Matrices read from the top, like the matrix itself: row 1 at the top, over its full size.
        if (data.Kind is PlotKind.Heatmap or PlotKind.Contour or PlotKind.Spy)
        {
            plot.Axes.SetLimits(0.5, data.Columns + 0.5, data.Rows + 0.5, 0.5);
        }

        // A square matrix, an image most likely (a radar screen, say), keeps square cells, zoomed out to show
        // all of it: circles stay round.
        if (data.Kind is PlotKind.Heatmap or PlotKind.Contour && data.Rows == data.Columns)
        {
            plot.Axes.Rules.Add(new ScottPlot.AxisRules.SquareZoomOut(plot.Axes.Bottom, plot.Axes.Left));
        }
    }

    /// <summary>
    /// How wide each bar is: 80% of the closest two bars' distance, so none overlap and a gap shows between them, even
    /// with uneven x values; 0.8 for a single bar.
    /// </summary>
    public static double BarWidth(double[] x)
    {
        double[] sorted = [.. x.Where(double.IsFinite).Order()];
        double closest = sorted.Zip(sorted.Skip(1), (a, b) => b - a).Where(d => d > 0).DefaultIfEmpty(1).Min();
        return closest * 0.8;
    }

    /// <summary>
    /// Shows <paramref name="data"/> in place of <paramref name="shown"/>, drawn in <paramref name="plot"/>, when
    /// both are heatmaps of the same size: only the numbers change, the colour bar, axes and zoom stay, so a
    /// heatmap drawn again and again (live, in a stream) changes smoothly. False when it needs drawing anew.
    /// </summary>
    public static bool Update(Plot plot, PlotData? shown, PlotData? data)
    {
        if (shown is not { Kind: PlotKind.Heatmap, Values: double[,] before } || data is not { Kind: PlotKind.Heatmap, Values: double[,] after }
            || shown.Rows != data.Rows || shown.Columns != data.Columns
            || before.GetLength(0) != after.GetLength(0) || before.GetLength(1) != after.GetLength(1)
            || plot.GetPlottables().OfType<ScottPlot.Plottables.Heatmap>().SingleOrDefault() is not ScottPlot.Plottables.Heatmap heatmap)
        {
            return false;
        }

        heatmap.Intensities = after;
        heatmap.Update();
        return true;
    }

    /// <summary>
    /// Text shown on contour plots of a matrix with one value only (or none): there are no lines.
    /// </summary>
    public static string FlatContours(double min) => double.IsNaN(min)
        ? "No contour lines: the matrix has no finite values."
        : $"No contour lines: every value is {PlotReadout.Number(min)}.";

    /// <summary>
    /// The lines of each level, in its colour from the heatmap's colour map; on a matrix as a heatmap's cells are
    /// (rows and columns numbered from 1, even when averaged down), with the colour bar in the larger window.
    /// </summary>
    private static void Contours(Plot plot, PlotData data, long count, Color foreground, bool compact)
    {
        double[,] values = data.Values!;
        (double min, double max) = ContourLines.Range(values);
        double[] levels = ContourLines.Levels(min, max, (int)count);
        if (levels.Length == 0)
        {
            ScottPlot.Plottables.Annotation note = plot.Add.Annotation(FlatContours(min), Alignment.MiddleCenter);
            note.LabelFontColor = foreground;
            note.LabelFontSize = compact ? 10 : 14;
            note.LabelBackgroundColor = Colors.Transparent;
            note.LabelBorderColor = Colors.Transparent;
            note.LabelShadowColor = Colors.Transparent;
            return;
        }

        double columnScale = (double)data.Columns / values.GetLength(1), rowScale = (double)data.Rows / values.GetLength(0);
        ScottPlot.Colormaps.Viridis colormap = new();
        foreach (double level in levels)
        {
            // All of a level's lines in one plottable, NaN between them: a straight path leaves a gap there.
            List<double> xs = [], ys = [];
            foreach (List<(double Column, double Row)> line in ContourLines.Lines(values, level))
            {
                foreach ((double column, double row) in line)
                {
                    xs.Add(((column + 0.5) * columnScale) + 0.5);
                    ys.Add(((row + 0.5) * rowScale) + 0.5);
                }

                xs.Add(double.NaN);
                ys.Add(double.NaN);
            }

            if (xs.Count > 0)
            {
                ScottPlot.Plottables.Scatter lines = plot.Add.ScatterLine(xs.ToArray(), ys.ToArray(), colormap.GetColor((level - min) / (max - min)));
                lines.LineWidth = compact ? 1.2f : 1.8f;            }
        }

        if (!compact)
        {
            _ = plot.Add.ColorBar(new Scale(colormap, min, max));
        }
    }

    /// <summary>
    /// What the colour bar of contour lines shows: the colour map from the smallest value to the largest.
    /// </summary>
    private sealed class Scale(IColormap colormap, double min, double max) : IHasColorAxis
    {
        public IColormap Colormap => colormap;

        public Range GetRange() => new(min, max);
    }

    /// <summary>
    /// Smaller points the more there are, so a dense cloud still shows as points rather than a solid area.
    /// </summary>
    private static float MarkerSize(int count, bool compact)
    {
        return count > 10_000 ? 2 : count > 1_000 ? 3 : compact ? 5 : 7;
    }

    private static void Style(Plot plot, Color foreground, bool compact)
    {
        plot.FigureBackground.Color = Colors.Transparent;
        // A light area behind dark text (pastel nodes, light windows), a dark one behind light text.
        bool darkText = (0.299 * foreground.R) + (0.587 * foreground.G) + (0.114 * foreground.B) < 128;
        plot.DataBackground.Color = darkText ? Colors.White.WithAlpha(0.4) : Colors.Black.WithAlpha(0.2);
        plot.Axes.Color(foreground);
        plot.Grid.MajorLineColor = foreground.WithAlpha(0.15);

        float size = compact ? 9 : 12;
        foreach (ScottPlot.IAxis axis in new ScottPlot.IAxis[] { plot.Axes.Bottom, plot.Axes.Left })
        {
            axis.TickLabelStyle.FontSize = size;
        }

        plot.Axes.Title.Label.FontSize = compact ? 11 : 16;
        plot.Axes.Title.Label.ForeColor = foreground;
    }
}
