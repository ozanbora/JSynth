using JuliaSynth.Runtime;
using ScottPlot;
using System.Linq;

namespace JuliaSynth.Controls;

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
    public static void Draw(Plot plot, PlotData? data, string title, Color foreground, bool compact)
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

            case PlotKind.Spy:
                ScottPlot.Plottables.Scatter dots = plot.Add.ScatterPoints(data.X, data.Y);
                dots.MarkerSize = MarkerSize(data.X.Length, compact) - 1;
                break;
        }

        plot.Axes.AutoScale();

        // Matrices read from the top, like the matrix itself: row 1 at the top, over its full size.
        if (data.Kind is PlotKind.Heatmap or PlotKind.Spy)
        {
            plot.Axes.SetLimits(0.5, data.Columns + 0.5, data.Rows + 0.5, 0.5);
        }

        // A square matrix, an image most likely (a radar screen, say), keeps square cells, zoomed out to show
        // all of it: circles stay round.
        if (data.Kind == PlotKind.Heatmap && data.Rows == data.Columns)
        {
            plot.Axes.Rules.Add(new ScottPlot.AxisRules.SquareZoomOut(plot.Axes.Bottom, plot.Axes.Left));
        }
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
