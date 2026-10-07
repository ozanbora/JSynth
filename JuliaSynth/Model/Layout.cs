using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// Where nodes sit on the canvas, in its cells, and arranging them: drawn together, apart from each other.
    /// </summary>
    public static class Layout
    {
        // Cells left between nodes: room for the wires, and no more.
        private const int RowGap = 2;
        private const int ColumnGap = 4;

        /// <summary>
        /// Where a node sits, in cells: centred on its cell, 9×6 cells (a Display 13×9, a Plot 16×13); nodes with a row
        /// per input grow down a cell per input past the second.
        /// </summary>
        public static (double Top, double Left, double Bottom, double Right) Footprint(Node node)
        {
            (double width, double height) = node switch
            {
                DisplayNode => (13, 9),
                PlotNode => (16, 13),
                _ => (9, 6),
            };
            double top = node.Row + 0.5 - (height / 2);
            if (node is not (DisplayNode or PlotNode) && node.Inputs.Count > 2)
            {
                height += node.Inputs.Count - 2;
            }

            return (top, node.Column + 0.5 - (width / 2), top + height, node.Column + 0.5 + (width / 2));
        }

        /// <summary>
        /// Arranges <paramref name="nodes"/>: the rows of nodes from the top, then the columns from the left, each as
        /// near the ones before it as the nodes beside it allow, a gap away: empty space closed up, and nodes on top of
        /// each other moved apart. Nodes keep their order, and those lined up stay so; the topmost and leftmost stay
        /// where they are.
        /// </summary>
        public static void Arrange(IList<Node> nodes)
        {
            if (nodes.Count == 0)
            {
                return;
            }

            Arrange(nodes, n => n.Row, (n, row) => n.Row = row, n => (Footprint(n).Top, Footprint(n).Bottom), n => (Footprint(n).Left, Footprint(n).Right), RowGap);
            Arrange(nodes, n => n.Column, (n, column) => n.Column = column, n => (Footprint(n).Left, Footprint(n).Right), n => (Footprint(n).Top, Footprint(n).Bottom), ColumnGap);
        }

        /// <summary>
        /// Arranges the nodes along one direction: in order of their place along it (<paramref name="place"/>), each
        /// band of nodes at the same place moves to <paramref name="gap"/> cells after the nodes before it that are
        /// beside it (<paramref name="across"/> overlapping), or to where the first band starts. Nodes at the same place
        /// on top of each other go in bands one after the other.
        /// </summary>
        private static void Arrange(IList<Node> nodes, Func<Node, int> place, Action<Node, int> move, Func<Node, (double Start, double End)> along,
            Func<Node, (double Start, double End)> across, int gap)
        {
            bool Beside(Node a, Node b) => across(a).Start < across(b).End && across(b).Start < across(a).End;

            // A band holds no two nodes beside each other: those go in a band after it.
            List<List<Node>> bands = [];
            foreach (IGrouping<int, Node> same in nodes.GroupBy(place).OrderBy(g => g.Key))
            {
                List<List<Node>> here = [];
                foreach (Node node in same.OrderBy(n => across(n).Start))
                {
                    (here.FirstOrDefault(b => !b.Any(other => Beside(node, other))) ?? AddBand(here)).Add(node);
                }

                bands.AddRange(here);
            }

            static List<Node> AddBand(List<List<Node>> here)
            {
                List<Node> band = [];
                here.Add(band);
                return band;
            }

            List<Node> placed = [];
            double first = nodes.Min(n => along(n).Start);
            foreach (List<Node> band in bands)
            {
                // Where the band can start: where the first did, or a gap after the end of a node beside it.
                double start = band.Min(n => along(n).Start);
                double wanted = first;
                foreach (Node node in band)
                {
                    foreach (Node before in placed.Where(p => Beside(node, p)))
                    {
                        wanted = Math.Max(wanted, along(before).End + gap - (along(node).Start - start));
                    }
                }

                // Back to close up space, or on to move off the nodes before it.
                int shift = (int)Math.Floor(start - wanted);
                foreach (Node node in band)
                {
                    move(node, place(node) - shift);
                }

                placed.AddRange(band);
            }
        }
    }
}
