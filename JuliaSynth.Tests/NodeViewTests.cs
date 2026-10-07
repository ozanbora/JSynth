using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The nodes drawn with the shared body (Only when, Window, Zip, Merge, Map): their parts, and their own buttons.
    /// </summary>
    public sealed class NodeViewTests
    {
        private static (Window Window, MainViewModel ViewModel) Show(params Node[] nodes)
        {
            MainViewModel.LoadStartingDesign();
            MainViewModel viewModel = new();
            viewModel.NewDesign();
            MainViewModel.Nodes.AddRange(nodes);
            Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1400, Height = 900 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return (window, viewModel);
        }

        [Fact]
        public void Each_shows_its_glyph_title_and_ports_and_the_merge_its_folder_output()
        {
            Ui.Run(() =>
            {
                WindowNode window = new() { Size = 6, Row = 10, Column = 10 };
                MergeNode merge = new() { Row = 10, Column = 30 };
                MapNode map = new() { InParallel = true, Row = 30, Column = 10 };
                (Window shown, _) = Show(window, merge, map);

                Controls.NodeBody[] bodies = [.. shown.GetVisualDescendants().OfType<Controls.NodeBody>()];
                Assert.Equal(
                    [("⧗", "last 6"), ("∪", "merge"), ("↦", "map ∥")],
                    bodies.Select(b => (b.Glyph, b.Title)).OrderBy(b => b.Glyph != "⧗").ThenBy(b => b.Glyph != "∪"));

                // The merge: its one input, its value and its folder output.
                Controls.Connector[] ports = [.. bodies.Single(b => b.Glyph == "∪").GetVisualDescendants().OfType<Controls.Connector>()];
                Assert.Equal(1, ports.Count(p => p.IsInput));
                Assert.Equal([null, MergeNode.FolderOutput], ports.Where(p => !p.IsInput).Select(p => p.Output));
                shown.Close();
            });
        }

        [Fact]
        public void The_windows_edit_button_sets_how_many_values_it_keeps_and_delete_removes_a_node()
        {
            Ui.Run(() =>
            {
                WindowNode window = new() { Size = 6, Row = 10, Column = 10 };
                (Window shown, _) = Show(window);
                Controls.NodeBody body = shown.GetVisualDescendants().OfType<Controls.NodeBody>().Single();

                Button edit = body.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Ξ");
                edit.Flyout!.ShowAt(edit);
                Dispatcher.UIThread.RunJobs();
                NumericUpDown size = ((Flyout)edit.Flyout).Content is StackPanel panel ? panel.Children.OfType<NumericUpDown>().Single() : throw new Xunit.Sdk.XunitException("No size box.");
                size.Value = 12;
                Assert.Equal(12, window.Size);
                edit.Flyout.Hide();

                Button delete = body.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "✕");
                delete.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.DoesNotContain(window, MainViewModel.Nodes);
                shown.Close();
            });
        }
    }
}
