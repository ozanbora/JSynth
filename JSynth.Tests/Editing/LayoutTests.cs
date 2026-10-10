using Avalonia.Threading;
using JSynth.Model;
using JSynth.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Editing
{
    /// <summary>
    /// Arranging nodes: empty space closed up, nodes on top of each other moved apart, their order and lines kept.
    /// </summary>
    public sealed class LayoutTests
    {
        private static bool Overlap(Node a, Node b)
        {
            (double top, double left, double bottom, double right) = Layout.Footprint(a);
            (double otherTop, double otherLeft, double otherBottom, double otherRight) = Layout.Footprint(b);
            return top < otherBottom && otherTop < bottom && left < otherRight && otherLeft < right;
        }

        [Fact]
        public void Far_apart_nodes_are_drawn_together_a_gap_away_in_their_order_and_lines()
        {
            // A row of three, far apart; the third with one far below it.
            Float a = new() { Row = 10, Column = 10 };
            Float b = new() { Row = 10, Column = 60 };
            Float c = new() { Row = 10, Column = 150 };
            Float below = new() { Row = 80, Column = 150 };
            Layout.Arrange(new List<Node> { a, b, c, below });

            // The first stays; the others 9 + 4 cells on (a node's width, the gap), still in a line; the one below 6 + 2.
            Assert.Equal([(10, 10), (10, 23), (10, 36), (18, 36)], new[] { a, b, c, below }.Select(n => (n.Row, n.Column)));
        }

        [Fact]
        public void Nodes_on_top_of_each_other_are_moved_apart()
        {
            List<Node> nodes = [.. Enumerable.Range(0, 6).Select(i => (Node)new Float { Row = 20 + i, Column = 20 + i })];
            nodes.Add(new DisplayNode { Row = 22, Column = 22 });
            Layout.Arrange(nodes);

            Assert.All(nodes, a => Assert.All(nodes.Where(b => b != a), b => Assert.False(Overlap(a, b))));
            Assert.Equal((20, 20), (nodes[0].Row, nodes[0].Column));
        }

        [Fact]
        public void Any_design_comes_out_with_no_node_on_another()
        {
            // 200 designs of 15 nodes of every size, crowded and scattered at random.
            System.Random random = new(7);
            for (int design = 0; design < 200; design++)
            {
                List<Node> nodes = [.. Enumerable.Range(0, 15).Select(_ => random.Next(4) switch
                {
                    0 => (Node)new DisplayNode(),
                    1 => new PlotNode(),
                    2 => new MergeNode(),
                    _ => Function(random.Next(1, 6)),
                })];
                int spread = design % 2 == 0 ? 20 : 120;
                foreach (Node node in nodes)
                {
                    (node.Row, node.Column) = (10 + random.Next(spread), 10 + random.Next(spread));
                }

                Layout.Arrange(nodes);
                Assert.All(nodes, a => Assert.All(nodes.Where(b => b != a), b => Assert.False(Overlap(a, b), $"design {design}")));
            }

            static Function Function(int parameters)
            {
                Function function = new() { Name = "f" };
                for (int i = 0; i < parameters; i++)
                {
                    function.Parameters.Add(new Parameter($"x{i}", JSynth.Model.Function.AnyType));
                }

                return function;
            }
        }

        [Fact]
        public void The_command_arranges_the_design_and_one_undo_puts_it_back()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.NewDesign();
                Float a = new() { Row = 10, Column = 10 };
                Float b = new() { Row = 10, Column = 90 };
                MainViewModel.Nodes.AddRange([a, b]);
                MainViewModel.History.Commit();

                ((System.Windows.Input.ICommand)viewModel.ArrangeNodes).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(23, b.Column);

                MainViewModel.History.Commit();
                ((System.Windows.Input.ICommand)viewModel.Undo).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(90, MainViewModel.Nodes.Single(n => n.ID == b.ID).Column);
            });
        }
    }
}
