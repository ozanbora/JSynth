using JSynth.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Controls;

/// <summary>
/// Where a surface is looked at from: turned <see cref="Azimuth"/> degrees around the vertical, from
/// <see cref="Elevation"/> degrees above the grid (below it when negative), zoomed in <see cref="Zoom"/> times.
/// </summary>
public readonly record struct SurfaceCamera(double Azimuth, double Elevation, double Zoom)
{
    /// <summary>
    /// The view a surface is first shown from, and goes back to: from above the corner where the matrix starts, row 1
    /// and column 1, nearest; the axes meet there and count away from the viewer.
    /// </summary>
    public static SurfaceCamera Start { get; } = new(35, 30, 1);

    /// <summary>
    /// Turned further around, and up or down (from straight above to straight below at most).
    /// </summary>
    public SurfaceCamera Turned(double around, double up)
    {
        double azimuth = Math.IEEERemainder(Azimuth + around, 360);
        return this with { Azimuth = azimuth, Elevation = Math.Clamp(Elevation + up, -90, 90) };
    }

    public SurfaceCamera Zoomed(double factor)
    {
        return this with { Zoom = Math.Clamp(Zoom * factor, 0.25, 8) };
    }

    /// <summary>
    /// A point of the box the surface is drawn in (x and y from -1 to 1, z within them) seen on a view
    /// <paramref name="width"/> × <paramref name="height"/>: its place, in pixels from the top left, and how near
    /// it is (larger is nearer). Unturned, x runs to the right, z up and y away.
    /// </summary>
    public (double X, double Y, double Depth) Project(double x, double y, double z, double width, double height)
    {
        double around = Azimuth * Math.PI / 180, up = Elevation * Math.PI / 180;
        double across = (x * Math.Cos(around)) - (y * Math.Sin(around));
        double away = (x * Math.Sin(around)) + (y * Math.Cos(around));
        double height3d = (away * Math.Sin(up)) + (z * Math.Cos(up));
        double depth = (z * Math.Sin(up)) - (away * Math.Cos(up));

        // The box, turned any way, fits with room for its labels.
        double scale = Zoom * Math.Min(width, height) / 3.1;
        return ((width / 2) + (across * scale), (height / 2) - (height3d * scale), depth);
    }
}

/// <summary>
/// A matrix as a surface: its grid in a box, columns from x = -1 to 1, rows from y = -1 to 1, the values from
/// z = -<see cref="Height"/> (the smallest) to <see cref="Height"/> (the largest); what is drawn of it from a
/// <see cref="SurfaceCamera"/>, in which order, and what is under the mouse.
/// </summary>
public sealed class SurfaceMesh
{
    /// <summary>
    /// How high the box is, for its width of 2.
    /// </summary>
    public const double Height = 0.6;

    private readonly PlotData _data;
    private readonly double[,] _values;
    private readonly int _block;

    public SurfaceMesh(PlotData data)
    {
        _data = data;
        _values = data.Values!;
        (Rows, Columns) = (_values.GetLength(0), _values.GetLength(1));
        (Min, Max) = ContourLines.Range(_values);

        // Averaged down in square blocks of this many rows and columns (see PlotData.Prelude).
        _block = (int)Math.Max(1, Math.Ceiling((double)Math.Max(data.Rows, data.Columns) / Math.Max(Rows, Columns)));
    }

    /// <summary>
    /// The grid's size, as shown (averaged down).
    /// </summary>
    public int Rows { get; }

    public int Columns { get; }

    /// <summary>
    /// The smallest and largest finite value; NaN when there are none.
    /// </summary>
    public double Min { get; }

    public double Max { get; }

    public double Value(int row, int column) => _values[row, column];

    /// <summary>
    /// The matrix's rows that grid row <paramref name="row"/> (from 0) stands for, numbered from 1: one, or the
    /// block averaged into it.
    /// </summary>
    public (long First, long Last) RowsOf(int row) => ((row * _block) + 1, Math.Min((row + 1) * _block, _data.Rows));

    public (long First, long Last) ColumnsOf(int column) => ((column * _block) + 1, Math.Min((column + 1) * _block, _data.Columns));

    /// <summary>
    /// Where row <paramref name="row"/> of the matrix (from 1, between the first and last grid row's) is along y.
    /// </summary>
    public double Y(double row) => Along(row, Middle(RowsOf(0)), Middle(RowsOf(Rows - 1)));

    public double X(double column) => Along(column, Middle(ColumnsOf(0)), Middle(ColumnsOf(Columns - 1)));

    /// <summary>
    /// How high <paramref name="value"/> is: a surface of one value lies flat in the middle.
    /// </summary>
    public double Z(double value) => Max > Min ? Height * ((2 * (value - Min) / (Max - Min)) - 1) : 0;

    /// <summary>
    /// How far up from the smallest value to the largest <paramref name="value"/> is, from 0 to 1: its colour.
    /// </summary>
    public double Fraction(double value) => Max > Min ? (value - Min) / (Max - Min) : 0.5;

    /// <summary>
    /// Every grid point on a view <paramref name="width"/> × <paramref name="height"/>, [row, column]; NaN for
    /// a value that isn't finite.
    /// </summary>
    public (double X, double Y, double Depth)[,] Project(SurfaceCamera camera, double width, double height)
    {
        (double, double, double)[,] points = new (double, double, double)[Rows, Columns];
        for (int i = 0; i < Rows; i++)
        {
            double y = Y(Middle(RowsOf(i)));
            for (int j = 0; j < Columns; j++)
            {
                double value = _values[i, j];
                points[i, j] = double.IsFinite(value)
                    ? camera.Project(X(Middle(ColumnsOf(j))), y, Z(value), width, height)
                    : (double.NaN, double.NaN, double.NaN);
            }
        }

        return points;
    }

    /// <summary>
    /// The squares between four neighbouring grid points, each by its first row and column, from the farthest to
    /// the nearest: drawn in this order (the painter's), the near ones cover the far ones. Squares with a value
    /// that isn't finite are left out.
    /// </summary>
    public List<(int Row, int Column)> Order((double X, double Y, double Depth)[,] points)
    {
        List<(double Depth, int Row, int Column)> squares = new((Rows - 1) * (Columns - 1));
        for (int i = 0; i + 1 < Rows; i++)
        {
            for (int j = 0; j + 1 < Columns; j++)
            {
                double depth = points[i, j].Depth + points[i, j + 1].Depth + points[i + 1, j].Depth + points[i + 1, j + 1].Depth;
                if (!double.IsNaN(depth))
                {
                    squares.Add((depth, i, j));
                }
            }
        }

        squares.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        return [.. squares.Select(s => (s.Row, s.Column))];
    }

    /// <summary>
    /// The grid point nearest to (<paramref name="x"/>, <paramref name="y"/>) on the nearest square under it, as
    /// drawn in <paramref name="order"/>; null off the surface.
    /// </summary>
    public (int Row, int Column)? PointAt((double X, double Y, double Depth)[,] points, IReadOnlyList<(int Row, int Column)> order, double x, double y)
    {
        for (int k = order.Count - 1; k >= 0; k--)
        {
            (int i, int j) = order[k];
            (int, int)[] corners = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)];

            // Drawn as two triangles, so looked for in those.
            if (Inside(points, x, y, corners[0], corners[1], corners[2]) || Inside(points, x, y, corners[0], corners[2], corners[3]))
            {
                return corners.MinBy(c => Math.Pow(points[c.Item1, c.Item2].X - x, 2) + Math.Pow(points[c.Item1, c.Item2].Y - y, 2));
            }
        }

        return null;
    }

    /// <summary>
    /// What the readout says of grid point <paramref name="row"/>, <paramref name="column"/>: where in the matrix
    /// it is, and its value.
    /// </summary>
    public string Describe(int row, int column)
    {
        double value = _values[row, column];
        if (_block == 1)
        {
            return $"row {row + 1}, column {column + 1}: {PlotReadout.Number(value)}";
        }

        (long firstRow, long lastRow) = RowsOf(row);
        (long firstColumn, long lastColumn) = ColumnsOf(column);
        return $"rows {firstRow}–{lastRow}, columns {firstColumn}–{lastColumn}: ≈ {PlotReadout.Number(value)}   (an average: the matrix is shown averaged down)";
    }

    /// <summary>
    /// Round values from <paramref name="min"/> to <paramref name="max"/> to mark an axis with, about
    /// <paramref name="count"/> of them; whole numbers only for rows and columns.
    /// </summary>
    public static double[] Ticks(double min, double max, int count, bool whole)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            return [];
        }

        if (max <= min)
        {
            return [min];
        }

        double step = ContourLines.RoundStep((max - min) / count);
        if (whole)
        {
            step = Math.Max(1, step);
        }

        int digits = Math.Clamp(1 - (int)Math.Floor(Math.Log10(step)), 0, 15);
        double slack = step * 1e-9;
        long first = (long)Math.Ceiling((min - slack) / step), last = (long)Math.Floor((max + slack) / step);
        return [.. Enumerable.Range(0, (int)(last - first + 1)).Select(k => Math.Round((first + k) * step, digits))];
    }

    private static double Middle((long First, long Last) block) => (block.First + block.Last) / 2.0;

    private static double Along(double at, double first, double last) => last > first ? -1 + (2 * (at - first) / (last - first)) : 0;

    private static bool Inside((double X, double Y, double Depth)[,] points, double x, double y, (int, int) a, (int, int) b, (int, int) c)
    {
        (double ax, double ay, _) = points[a.Item1, a.Item2];
        (double bx, double by, _) = points[b.Item1, b.Item2];
        (double cx, double cy, _) = points[c.Item1, c.Item2];

        // Seen edge on, a triangle covers nothing.
        if (Math.Abs(((bx - ax) * (cy - ay)) - ((by - ay) * (cx - ax))) < 1e-9)
        {
            return false;
        }

        double d1 = ((x - bx) * (ay - by)) - ((ax - bx) * (y - by));
        double d2 = ((x - cx) * (by - cy)) - ((bx - cx) * (y - cy));
        double d3 = ((x - ax) * (cy - ay)) - ((cx - ax) * (y - ay));
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }
}
