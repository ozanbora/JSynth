using JSynth.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JSynth.Controls;

/// <summary>
/// What a plot shows where the mouse points: the coordinates, and the data there: the nearest point of a
/// line or scatter plot, the bar or histogram bin under it, the value of a heatmap or contour plot's cell (and the
/// nearest contour level), whether a sparse matrix stores an entry.
/// </summary>
public sealed class PlotReadout
{
    /// <summary>
    /// Shown while the mouse isn't over the data.
    /// </summary>
    public const string Hint = "Point at the plot to see the coordinates. Wheel: zoom, drag: pan, right-click: more.";

    /// <summary>
    /// Shown below a surface while the mouse isn't on it.
    /// </summary>
    public const string SurfaceHint = "Point at the surface to see its values. Drag: turn, wheel: zoom, double-click: back to the start.";

    private readonly PlotData _data;
    private readonly double[] _levels = [];
    private HashSet<(long Row, long Column)>? _stored;

    /// <param name="levels">Contour lines: how many levels, as drawn (see <see cref="ContourLines.Levels"/>).</param>
    public PlotReadout(PlotData data, long levels = 0)
    {
        _data = data;
        if (data is { Kind: PlotKind.Contour, Values: double[,] values })
        {
            (double min, double max) = ContourLines.Range(values);
            _levels = ContourLines.Levels(min, max, (int)levels);
        }
    }

    /// <summary>
    /// The text for the mouse at (<paramref name="x"/>, <paramref name="y"/>), in plot coordinates, and the
    /// data point to mark there, if any.
    /// </summary>
    /// <param name="xSpan">The width of the visible x range, so nearness counts both axes alike.</param>
    /// <param name="ySpan">The height of the visible y range.</param>
    public (string Text, (double X, double Y)? Point) Describe(double x, double y, double xSpan, double ySpan)
    {
        switch (_data.Kind)
        {
            case PlotKind.Line or PlotKind.Scatter when Nearest(x, y, xSpan, ySpan) is int i:
                return ($"x {Number(x)}   y {Number(y)}      point {i + 1}: ({Number(_data.X[i])}, {Number(_data.Y[i])})", (_data.X[i], _data.Y[i]));

            case PlotKind.Bar when Bar(x, y) is int b:
                return ($"x {Number(x)}   y {Number(y)}      bar {b + 1}: x {Number(_data.X[b])}, value {Number(_data.Y[b])}", (_data.X[b], _data.Y[b]));

            case PlotKind.Histogram when Bin(x, y) is int b:
                return ($"x {Number(x)}   y {Number(y)}      {Number(_data.X[b])} – {Number(_data.X[b + 1])}: {Number(_data.Y[b])}", ((_data.X[b] + _data.X[b + 1]) / 2, _data.Y[b]));

            case PlotKind.Heatmap or PlotKind.Contour when Cell(x, y) is (long row, long column):
            {
                double[,] values = _data.Values!;
                int shownRows = values.GetLength(0), shownColumns = values.GetLength(1);
                double value = values[(int)((row - 1) * shownRows / _data.Rows), (int)((column - 1) * shownColumns / _data.Columns)];
                bool averaged = shownRows < _data.Rows || shownColumns < _data.Columns;
                string level = _data.Kind == PlotKind.Contour && NearestLevel(value) is double nearest ? $"   nearest level {Number(nearest)}" : string.Empty;
                return ($"row {row}, column {column}: {(averaged ? "≈ " : string.Empty)}{Number(value)}{level}{(averaged ? "   (an average: the matrix is shown averaged down)" : string.Empty)}", null);
            }

            case PlotKind.Spy when Cell(x, y) is (long row, long column):
            {
                bool stored = Stored().Contains((row, column));
                return ($"row {row}, column {column}: {(stored ? "stored" : "not stored")}", stored ? (column, row) : null);
            }

            default:
                return ($"x {Number(x)}   y {Number(y)}", null);
        }
    }

    /// <summary>
    /// The index of the data point nearest to (<paramref name="x"/>, <paramref name="y"/>), measuring each
    /// axis in parts of its visible span; null without points.
    /// </summary>
    public int? Nearest(double x, double y, double xSpan, double ySpan)
    {
        double sx = xSpan > 0 ? xSpan : 1, sy = ySpan > 0 ? ySpan : 1;
        int? best = null;
        double bestDistance = double.PositiveInfinity;
        for (int i = 0; i < _data.X.Length; i++)
        {
            double dx = (_data.X[i] - x) / sx, dy = (_data.Y[i] - y) / sy;
            double distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (i, distance);
            }
        }

        return best;
    }

    /// <summary>
    /// The index of the bar under (<paramref name="x"/>, <paramref name="y"/>): across its width, between 0 and its
    /// top; null over none.
    /// </summary>
    public int? Bar(double x, double y)
    {
        double half = PlotRenderer.BarWidth(_data.X) / 2;
        for (int i = 0; i < _data.X.Length; i++)
        {
            if (Math.Abs(x - _data.X[i]) <= half && y >= Math.Min(0, _data.Y[i]) && y <= Math.Max(0, _data.Y[i]))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// The index of the histogram bin under (<paramref name="x"/>, <paramref name="y"/>): between its edges (the
    /// last one's upper edge counting in it, as its values do), from 0 to its count; null over none.
    /// </summary>
    public int? Bin(double x, double y)
    {
        int count = _data.Y.Length;
        for (int i = 0; i < count; i++)
        {
            bool across = x >= _data.X[i] && (x < _data.X[i + 1] || (i == count - 1 && x == _data.X[i + 1]));
            if (across && y >= 0 && y <= _data.Y[i])
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// The matrix cell (row, column, numbered from 1 as in Julia) under (<paramref name="x"/>, <paramref name="y"/>):
    /// columns run along x and rows along y; null outside the matrix.
    /// </summary>
    public (long Row, long Column)? Cell(double x, double y)
    {
        long row = (long)Math.Round(y), column = (long)Math.Round(x);
        return row >= 1 && row <= _data.Rows && column >= 1 && column <= _data.Columns ? (row, column) : null;
    }

    /// <summary>
    /// The contour level nearest to <paramref name="value"/>; null without levels or for a value that isn't finite.
    /// </summary>
    public double? NearestLevel(double value)
    {
        return _levels.Length == 0 || !double.IsFinite(value) ? null : _levels.MinBy(l => Math.Abs(l - value));
    }

    /// <summary>
    /// A number short enough to read at a glance: up to 6 significant digits.
    /// </summary>
    public static string Number(double value)
    {
        return value.ToString("G6", CultureInfo.InvariantCulture);
    }

    private HashSet<(long, long)> Stored()
    {
        if (_stored is null)
        {
            _stored = [];
            for (int i = 0; i < _data.X.Length; i++)
            {
                _ = _stored.Add(((long)_data.Y[i], (long)_data.X[i]));
            }
        }

        return _stored;
    }
}
