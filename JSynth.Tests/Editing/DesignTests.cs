using JSynth.Model;
using JSynth.ViewModels;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Editing
{
    /// <summary>
    /// Editing the design through the view model: wiring, adding, deleting, undo. Each test starts
    /// from the starting design g(f(x, z), n).
    /// </summary>
    public class DesignTests
    {
        private static Function F => MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");

        private static Function G => MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g");

        private static void Run(System.Action test)
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                test();
            });
        }

        [Fact]
        public void The_starting_design_is_fully_wired()
        {
            Run(() =>
            {
                Assert.Equal(5, MainViewModel.Nodes.Count);
                Assert.Equal(4, MainViewModel.Connections.Count);
                Assert.All(MainViewModel.Connections, c => Assert.True(Wiring.IsAttached(MainViewModel.Nodes, c)));
            });
        }

        [Fact]
        public void Connecting_an_input_replaces_its_wire()
        {
            Run(() =>
            {
                Parameter y = G.Parameters[0];
                Float other = new() { SelectedType = "64-bit" };
                MainViewModel.Nodes.Add(other);

                Assert.Null(MainViewModel.Connect(other, G, y));

                Assert.Equal(other.ID, MainViewModel.ConnectionTo(y)?.SourceID);
                Assert.Equal(4, MainViewModel.Connections.Count);
            });
        }

        [Fact]
        public void A_rejected_connection_changes_nothing()
        {
            Run(() =>
            {
                Parameter y = G.Parameters[0];
                string sourceBefore = MainViewModel.ConnectionTo(y)!.SourceID;

                Assert.NotNull(MainViewModel.Connect(G, G, y));

                Assert.Equal(sourceBefore, MainViewModel.ConnectionTo(y)?.SourceID);
            });
        }

        [Fact]
        public void Deleting_a_node_or_a_parameter_removes_its_wires()
        {
            Run(() =>
            {
                F.RemoveParameter(F.Parameters[0]);
                Assert.Equal(3, MainViewModel.Connections.Count);

                MainViewModel.SelectOnly(G);
                MainViewModel.DeleteSelectedNodes();
                Assert.Equal(4, MainViewModel.Nodes.Count);
                Assert.Single(MainViewModel.Connections);
            });
        }

        [Fact]
        public void An_added_node_lands_clear_of_the_others_and_is_selected()
        {
            Run(() =>
            {
                Node x = MainViewModel.Nodes[0];
                Node added = MainViewModel.AddNode(nameof(Float), x.Row, x.Column);

                Assert.Equal(6, MainViewModel.Nodes.Count);
                Assert.All(MainViewModel.Nodes.Where(n => n != added),
                    n => Assert.False(System.Math.Abs(n.Row - added.Row) < 7 && System.Math.Abs(n.Column - added.Column) < 10));
                Assert.Equal([added], MainViewModel.Nodes.Where(n => n.IsSelected));
            });
        }

        [Fact]
        public void Added_functions_get_a_free_name()
        {
            Run(() =>
            {
                Function added = Assert.IsType<Function>(MainViewModel.AddNode(nameof(Function), 40, 40));

                Assert.Equal("h", added.Name);
            });
        }

        [Fact]
        public void Undoing_a_delete_brings_back_the_node_and_its_wires_selected()
        {
            Run(() =>
            {
                MainViewModel.SelectOnly(G);
                MainViewModel.DeleteSelectedNodes();
                MainViewModel.History.Commit();

                MainViewModel.History.Undo();

                Assert.Equal(5, MainViewModel.Nodes.Count);
                Assert.Equal(4, MainViewModel.Connections.Count);
                Assert.True(G.IsSelected);
            });
        }

        [Fact]
        public void Edits_to_nodes_and_parameters_are_undoable()
        {
            Run(() =>
            {
                F.Row += 3;
                F.Parameters[0].Type = Function.AnyType;
                MainViewModel.History.Commit();

                MainViewModel.History.Undo();

                Assert.Equal(9, F.Row);
                Assert.Equal("Float64", F.Parameters[0].Type);
            });
        }

        [Fact]
        public void Selecting_is_not_an_undo_step()
        {
            Run(() =>
            {
                MainViewModel.SelectOnly(F);
                MainViewModel.History.Commit();

                Assert.False(MainViewModel.History.CanUndo);
            });
        }
    }
}
