using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests.Editing
{
    /// <summary>
    /// Cut, copy, paste and duplicate, starting from the starting design g(f(x, z), n).
    /// </summary>
    public class ClipboardTests
    {
        private static Node X => MainViewModel.Nodes.OfType<Float>().First();

        private static Function F => MainViewModel.Nodes.OfType<Function>().First(n => n.Name == "f");

        private static void Run(System.Action test)
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                test();
            });
        }

        private static void Select(params Node[] nodes)
        {
            MainViewModel.SelectOnly(null);
            foreach (Node node in nodes)
            {
                node.IsSelected = true;
            }
        }

        [Fact]
        public void Pasted_nodes_are_new_nodes_that_keep_only_the_wires_between_them()
        {
            Run(() =>
            {
                Node x = X;
                Function f = F;
                Select(x, f);
                Assert.True(MainViewModel.CopySelection());

                MainViewModel.PasteClipboard();

                Assert.Equal(7, MainViewModel.Nodes.Count);
                Node[] pasted = MainViewModel.Nodes.Where(n => n.IsSelected).ToArray();
                Assert.Equal(2, pasted.Length);
                Assert.DoesNotContain(x, pasted);
                Assert.DoesNotContain(f, pasted);
                Assert.Equal(MainViewModel.Nodes.Count, MainViewModel.Nodes.Select(n => n.ID).Distinct().Count());

                // x -> f is inside the copy, so it comes along; z -> f is not.
                Function copyOfF = pasted.OfType<Function>().Single();
                Node copyOfX = pasted.OfType<Float>().Single();
                Connection wire = Assert.Single(MainViewModel.Connections, c => c.TargetID == copyOfF.ID);
                Assert.Equal(copyOfX.ID, wire.SourceID);
                Assert.NotEqual(f.Parameters[0].ID, copyOfF.Parameters[0].ID);
            });
        }

        [Fact]
        public void A_pasted_function_gets_a_free_name()
        {
            Run(() =>
            {
                Select(F);
                MainViewModel.CopySelection();

                MainViewModel.PasteClipboard();

                Function copy = MainViewModel.Nodes.OfType<Function>().Single(n => n.IsSelected);
                Assert.Equal("h", copy.Name);
            });
        }

        [Fact]
        public void Repeated_pastes_cascade()
        {
            Run(() =>
            {
                Node x = X;
                Select(x);
                MainViewModel.CopySelection();

                MainViewModel.PasteClipboard();
                Node first = MainViewModel.Nodes.Single(n => n.IsSelected);
                MainViewModel.PasteClipboard();
                Node second = MainViewModel.Nodes.Single(n => n.IsSelected);

                Assert.Equal((x.Row + 3, x.Column + 3), (first.Row, first.Column));
                Assert.Equal((x.Row + 6, x.Column + 6), (second.Row, second.Column));
            });
        }

        [Fact]
        public void Cut_then_paste_puts_the_nodes_back_where_they_were()
        {
            Run(() =>
            {
                Node x = X;
                Function f = F;
                (int row, int column) = (f.Row, f.Column);
                Select(x, f);

                MainViewModel.CutSelection();
                Assert.Equal(3, MainViewModel.Nodes.Count);

                MainViewModel.PasteClipboard();
                Function back = MainViewModel.Nodes.OfType<Function>().Single(n => n.IsSelected);
                Assert.Equal((row, column), (back.Row, back.Column));
                Assert.Equal("f", back.Name);
            });
        }

        [Fact]
        public void Duplicate_leaves_the_clipboard_alone()
        {
            Run(() =>
            {
                Select(X);
                MainViewModel.CopySelection();

                Select(F);
                MainViewModel.DuplicateSelection();
                MainViewModel.PasteClipboard();

                Assert.Equal(7, MainViewModel.Nodes.Count);
                Assert.IsType<Float>(MainViewModel.Nodes.Single(n => n.IsSelected));
            });
        }

        [Fact]
        public void A_paste_is_one_undo_step()
        {
            Run(() =>
            {
                Select(X, F);
                MainViewModel.CopySelection();
                MainViewModel.PasteClipboard();
                MainViewModel.History.Commit();

                MainViewModel.History.Undo();

                Assert.Equal(5, MainViewModel.Nodes.Count);
                Assert.Equal(4, MainViewModel.Connections.Count);
            });
        }

        [Fact]
        public void Nothing_selected_copies_nothing()
        {
            Run(() =>
            {
                Select();

                Assert.False(MainViewModel.CopySelection());
            });
        }
    }
}
