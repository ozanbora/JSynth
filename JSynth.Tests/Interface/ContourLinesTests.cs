using JSynth.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// The lines of a contour plot, worked out by marching squares, and the levels they are drawn at.
    /// </summary>
    public sealed class ContourLinesTests
    {
        private static (double, double)[] Rounded(IEnumerable<(double Column, double Row)> points)
        {
            return [.. points.Select(p => (Math.Round(p.Column, 9), Math.Round(p.Row, 9)))];
        }

        [Fact]
        public void A_single_peak_gives_a_closed_ring_around_it()
        {
            double[,] peak = { { 0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 0 } };

            List<(double Column, double Row)> ring = Assert.Single(ContourLines.Lines(peak, 0.5));

            // Halfway from the peak to each neighbour, and back to the start.
            Assert.Equal(5, ring.Count);
            Assert.Equal(ring[0], ring[^1]);
            Assert.Equal(new[] { (0.5, 1.0), (1.0, 0.5), (1.0, 1.5), (1.5, 1.0) }, Rounded(ring.Skip(1)).Order());

            // A level the peak doesn't reach has none.
            Assert.Empty(ContourLines.Lines(peak, 2));
        }

        [Fact]
        public void A_slope_gives_straight_lines_across_it_where_it_crosses_each_level()
        {
            // Rising 0, 1, 2, 3 along each of 3 rows.
            double[,] slope = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    slope[i, j] = j;
                }
            }

            foreach (double level in new[] { 0.5, 1.5, 2.25 })
            {
                List<(double Column, double Row)> line = Assert.Single(ContourLines.Lines(slope, level));
                Assert.Equal(new[] { (level, 0.0), (level, 1.0), (level, 2.0) }, Rounded(line).Order());
            }
        }

        [Fact]
        public void A_saddle_is_cut_by_the_value_at_its_centre()
        {
            // Highs at the top left and bottom right; the centre is 0.5.
            double[,] saddle = { { 1, 0 }, { 0, 1 } };

            // Below the centre the highs join through it: the lows (top right, bottom left) are cut off.
            List<List<(double Column, double Row)>> below = ContourLines.Lines(saddle, 0.4);
            Assert.Equal(2, below.Count);
            Assert.Contains(below, l => Rounded(l).Order().SequenceEqual([(0.6, 0.0), (1.0, 0.4)]));
            Assert.Contains(below, l => Rounded(l).Order().SequenceEqual([(0.0, 0.6), (0.4, 1.0)]));

            // Above it the lows join: the highs are cut off.
            List<List<(double Column, double Row)>> above = ContourLines.Lines(saddle, 0.6);
            Assert.Equal(2, above.Count);
            Assert.Contains(above, l => Rounded(l).Order().SequenceEqual([(0.0, 0.4), (0.4, 0.0)]));
            Assert.Contains(above, l => Rounded(l).Order().SequenceEqual([(0.6, 1.0), (1.0, 0.6)]));
        }

        [Fact]
        public void Squares_with_a_missing_value_have_no_lines()
        {
            double[,] holed = { { 0, 1, 2 }, { 0, double.NaN, 2 } };

            Assert.Empty(ContourLines.Lines(holed, 0.5));
            Assert.Equal((0.0, 2.0), ContourLines.Range(holed));
        }

        [Fact]
        public void Auto_levels_are_about_ten_round_values_strictly_between_the_smallest_and_largest()
        {
            Assert.Equal(new double[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 }, ContourLines.Levels(0, 1, 0));
            Assert.Equal(new double[] { 10.0, 20, 30, 40, 50, 60, 70, 80, 90 }, ContourLines.Levels(0, 100, 0));
            Assert.Equal(new double[] { -2.0, -1, 0, 1, 2, 3, 4, 5, 6 }, ContourLines.Levels(-3, 7, 0));
            Assert.Equal(new double[] { 0.05, 0.1, 0.15, 0.2, 0.25 }, ContourLines.Levels(0, 0.3, 0));
            Assert.Equal(new double[] { 1000.0, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800 }, ContourLines.Levels(987, 1834, 0));
        }

        [Fact]
        public void A_number_of_levels_spaces_them_evenly_and_one_value_has_none()
        {
            Assert.Equal(new double[] { 2.0, 4, 6, 8 }, ContourLines.Levels(0, 10, 4));
            Assert.Equal(new double[] { 5.0 }, ContourLines.Levels(0, 10, 1));

            Assert.Empty(ContourLines.Levels(3, 3, 0));
            Assert.Empty(ContourLines.Levels(double.NaN, double.NaN, 5));
        }
    }
}
