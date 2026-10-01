using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JuliaSynth.Controls;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The function panel docked beside the designer, where functions are edited; other nodes keep
    /// their flyouts.
    /// </summary>
    public class InspectorTests
    {
        private static Function F => MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");

        private static Function G => MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g");

        private static void Run(System.Action<MainViewModel> test)
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                test(new MainViewModel());
            });
        }

        [Fact]
        public void Opening_a_function_shows_and_selects_it_and_closing_hides_the_panel()
        {
            Run(viewModel =>
            {
                Assert.False(viewModel.IsInspectorOpen);

                viewModel.Inspect(F);

                Assert.Same(F, viewModel.Inspected);
                Assert.True(viewModel.IsInspectorOpen);
                Assert.Equal([F], MainViewModel.Nodes.Where(n => n.IsSelected));

                ((System.Windows.Input.ICommand)viewModel.CloseInspector).Execute(null);
                Assert.Null(viewModel.Inspected);
            });
        }

        [Fact]
        public void While_open_the_panel_follows_the_function_selected_on_its_own()
        {
            Run(viewModel =>
            {
                viewModel.Inspect(F);

                MainViewModel.SelectOnly(G);
                Assert.Same(G, viewModel.Inspected);

                // Selecting another kind of node, or several, leaves the panel as it is.
                MainViewModel.SelectOnly(MainViewModel.Nodes.OfType<Float>().Single());
                Assert.Same(G, viewModel.Inspected);
                F.IsSelected = true;
                G.IsSelected = true;
                Assert.Same(G, viewModel.Inspected);
            });
        }

        [Fact]
        public void A_closed_panel_does_not_open_on_selection()
        {
            Run(viewModel =>
            {
                MainViewModel.SelectOnly(F);

                Assert.Null(viewModel.Inspected);
            });
        }

        [Fact]
        public void Undo_keeps_the_panel_on_the_function_and_deleting_it_closes_the_panel()
        {
            Run(viewModel =>
            {
                viewModel.Inspect(F);
                F.Body = "x + z";
                MainViewModel.History.Commit();

                // Undo rebuilds the nodes; the panel moves to the new f.
                MainViewModel.History.Undo();
                Assert.Same(F, viewModel.Inspected);
                Assert.Equal("x ^ z", viewModel.Inspected!.Body);

                MainViewModel.SelectOnly(F);
                MainViewModel.DeleteSelectedNodes();
                Assert.Null(viewModel.Inspected);
            });
        }

        [Fact]
        public void The_edit_button_of_a_function_opens_the_panel_and_small_nodes_keep_their_flyout()
        {
            Run(viewModel =>
            {
                Views.MainWindow window = new() { DataContext = viewModel, Width = 1400, Height = 900 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Views.Function node = window.GetLogicalDescendants().OfType<Views.Function>().Single(v => v.DataContext == G);
                Button edit = node.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Ξ");
                Assert.Null(edit.Flyout);
                edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();

                Assert.Same(G, viewModel.Inspected);
                Border panel = window.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "InspectorPanel");
                Assert.True(panel.IsVisible);
                Assert.Same(G, panel.GetLogicalDescendants().OfType<Views.Details.Function>().Single().DataContext);

                Views.Inputs.Float x = window.GetLogicalDescendants().OfType<Views.Inputs.Float>().Single();
                Assert.NotNull(x.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Ξ").Flyout);
                window.Close();
            });
        }

        [Fact]
        public void Typing_in_the_panel_never_edits_the_design_through_shortcuts()
        {
            Run(viewModel =>
            {
                Views.MainWindow window = new() { DataContext = viewModel, Width = 1400, Height = 900 };
                window.Show();
                viewModel.Inspect(G);
                Dispatcher.UIThread.RunJobs();

                JuliaCodeEditor editor = window.GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();
                Assert.True(editor.TextArea.Focus());
                editor.CaretOffset = editor.Text.Length;
                int nodes = MainViewModel.Nodes.Count;

                Assert.True(viewModel.IsTypingText);
                window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
                window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Assert.Equal("y / ", G.Body);

                // Ctrl+D is the editor's (delete the line), not Duplicate.
                window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");
                Assert.Equal(string.Empty, G.Body);

                // No node duplicated or deleted; the function stays selected and in the panel.
                Assert.Equal(nodes, MainViewModel.Nodes.Count);
                Assert.True(G.IsSelected);
                Assert.Same(G, viewModel.Inspected);

                // Back on the canvas, the shortcuts edit the design again.
                window.GetVisualDescendants().OfType<DesignerGrid>().Single().Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.False(viewModel.IsTypingText);
                window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");
                Assert.Equal(nodes + 1, MainViewModel.Nodes.Count);
                window.Close();
            });
        }
    }
}
