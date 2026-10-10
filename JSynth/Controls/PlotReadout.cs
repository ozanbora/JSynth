using JSynth.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace JSynth.Controls;

/// <summary>
/// What a plot shows where the mouse points: the coordinates, and the data there: the nearest point of a
/// line or scatter plot, the value of a heatmap cell, whether a sparse matrix stores an entry.
/// </summary>
public sealed class PlotReadout
{
    /// <summary>
    /// Shown while the mouse isn't over the data.
    /// </summary>
    public const string Hint = "Point at the plot to see the coordinates. Wheel: zoom, drag: pan, right-click: more.";

    private readonly PlotData _data;
    private HashSet<(long Row, long Column)>? _stored;

    public PlotReadout(PlotData data)
    {
        _data = data;
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

            case PlotKind.Heatmap when Cell(x, y) is (long row, long column):
            {
                double[,] values = _data.Values!;
                int shownRows = values.GetLength(0), shownColumns = values.GetLength(1);
                double value = values[(int)((row - 1) * shownRows / _data.Rows), (int)((column - 1) * shownColumns / _data.Columns)];
                bool averaged = shownRows < _data.Rows || shownColumns < _data.Columns;
                return ($"row {row}, column {column}: {(averaged ? "≈ " : string.Empty)}{Number(value)}{(averaged ? "   (an average: the matrix is shown averaged down)" : string.Empty)}", null);
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
    /// The matrix cell (row, column, numbered from 1 as in Julia) under (<paramref name="x"/>, <paramref name="y"/>):
    /// columns run along x and rows along y; null outside the matrix.
    /// </summary>
    public (long Row, long Column)? Cell(double x, double y)
    {
        long row = (long)Math.Round(y), column = (long)Math.Round(x);
        return row >= 1 && row <= _data.Rows && column >= 1 && column <= _data.Columns ? (row, column) : null;
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
