using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Controls;

/// <summary>
/// The lines where a matrix's values cross a level, by marching squares: the values sit on a grid, row i and
/// column j at (j, i); each square of four neighbours the level cuts gives a segment or two, and the segments
/// join into lines, closed rings around a peak or a pit.
/// </summary>
public static class ContourLines
{
    /// <summary>
    /// The levels to draw between <paramref name="min"/> and <paramref name="max"/>: <paramref name="count"/>
    /// evenly spaced ones, or for 0 about 10 round values (multiples of 1, 2 or 5 times a power of ten). None
    /// when the values are all the same (or not finite).
    /// </summary>
    public static double[] Levels(double min, double max, int count)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
        {
            return [];
        }

        if (count > 0)
        {
            return [.. Enumerable.Range(1, count).Select(i => min + ((max - min) * i / (count + 1)))];
        }

        double step = RoundStep((max - min) / 10);
        long first = (long)Math.Floor(min / step), last = (long)Math.Ceiling(max / step);

        // Rounded to the step's digits, so 0.3 is 0.3 and not 0.30000000000000004.
        int digits = Math.Clamp(1 - (int)Math.Floor(Math.Log10(step)), 0, 15);
        return [.. Enumerable.Range(0, (int)(last - first + 1)).Select(k => Math.Round((first + k) * step, digits)).Where(v => v > min && v < max)];
    }

    /// <summary>
    /// The smallest and largest finite value; NaN for both when there are none.
    /// </summary>
    public static (double Min, double Max) Range(double[,] values)
    {
        double[] finite = [.. values.Cast<double>().Where(double.IsFinite)];
        return finite.Length == 0 ? (double.NaN, double.NaN) : (finite.Min(), finite.Max());
    }

    /// <summary>
    /// The lines where <paramref name="values"/> cross <paramref name="level"/>, each a list of (column, row)
    /// points counted from 0; a closed one ends where it starts. Squares with a NaN corner have none.
    /// </summary>
    public static List<List<(double Column, double Row)>> Lines(double[,] values, double level)
    {
        int rows = values.GetLength(0), columns = values.GetLength(1);
        Dictionary<long, (double, double)> points = [];
        Dictionary<long, List<long>> joined = [];

        // The point where the level crosses the edge between two grid points, named by that edge, so the two
        // squares sharing it find the same one.
        long Crossing(int i0, int j0, int i1, int j1)
        {
            long key = ((((long)i0 * columns) + j0) * 2) + (i0 == i1 ? 0 : 1);
            if (!points.ContainsKey(key))
            {
                double v0 = values[i0, j0], v1 = values[i1, j1];
                double t = (level - v0) / (v1 - v0);
                points[key] = (j0 + (t * (j1 - j0)), i0 + (t * (i1 - i0)));
            }

            return key;
        }

        void Join(long a, long b)
        {
            (joined.TryGetValue(a, out List<long>? fromA) ? fromA : joined[a] = []).Add(b);
            (joined.TryGetValue(b, out List<long>? fromB) ? fromB : joined[b] = []).Add(a);
        }

        for (int i = 0; i + 1 < rows; i++)
        {
            for (int j = 0; j + 1 < columns; j++)
            {
                // The corners clockwise from the top left: above the level or not.
                double a = values[i, j], b = values[i, j + 1], c = values[i + 1, j + 1], d = values[i + 1, j];
                if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c) || double.IsNaN(d))
                {
                    continue;
                }

                int corners = (a > level ? 8 : 0) | (b > level ? 4 : 0) | (c > level ? 2 : 0) | (d > level ? 1 : 0);
                if (corners is 0 or 15)
                {
                    continue;
                }

                long Top() => Crossing(i, j, i, j + 1);
                long Right() => Crossing(i, j + 1, i + 1, j + 1);
                long Bottom() => Crossing(i + 1, j, i + 1, j + 1);
                long Left() => Crossing(i, j, i + 1, j);

                // A saddle (two opposite corners above) is cut by the value at its centre: when that is above,
                // the corners above join through it and the two below are cut off, and the other way round.
                bool centreAbove = (a + b + c + d) / 4 > level;
                switch (corners)
                {
                    case 1 or 14: Join(Left(), Bottom()); break;
                    case 2 or 13: Join(Bottom(), Right()); break;
                    case 3 or 12: Join(Left(), Right()); break;
                    case 4 or 11: Join(Top(), Right()); break;
                    case 6 or 9: Join(Top(), Bottom()); break;
                    case 7 or 8: Join(Left(), Top()); break;
                    case 5 when centreAbove:
                    case 10 when !centreAbove:
                        Join(Left(), Top());
                        Join(Bottom(), Right());
                        break;
                    case 5 or 10:
                        Join(Top(), Right());
                        Join(Left(), Bottom());
                        break;
                }
            }
        }

        // Each crossing joins one or two others: open lines run between the ones joining one (at the edge of the
        // matrix or a NaN), and what is left are rings.
        List<List<(double, double)>> lines = [];
        HashSet<long> done = [];
        foreach (long start in joined.Keys.OrderBy(k => joined[k].Count))
        {
            if (done.Contains(start))
            {
                continue;
            }

            List<(double, double)> line = [points[start]];
            _ = done.Add(start);
            for (long at = start; joined[at].FirstOrDefault(n => !done.Contains(n), -1) is long next and not -1; at = next)
            {
                line.Add(points[next]);
                _ = done.Add(next);
            }

            if (joined[start].Count == 2)
            {
                line.Add(points[start]);
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// A round step at least <paramref name="rough"/>: 1, 2 or 5 times a power of ten.
    /// </summary>
    private static double RoundStep(double rough)
    {
        double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double fraction = rough / power;
        return power * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);
    }
}
