using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;

namespace JSynth.Tests.Editing
{
    /// <summary>
    /// The node library: the selection saved under a name, added again as a copy of its own in any design.
    /// </summary>
    public sealed class LibraryTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public LibraryTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static string Script() => ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, false).Text;

        /// <summary>
        /// The starting design, z and f made one group, alone selected.
        /// </summary>
        private static GroupNode StartingWithAGroup()
        {
            MainViewModel.LoadStartingDesign();
            MainViewModel.SelectOnly(MainViewModel.Nodes.OfType<Integer>().Single());
            MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f").IsSelected = true;
            return MainViewModel.GroupSelection()!;
        }

        [Fact]
        public void A_group_saved_to_the_library_is_added_in_another_design_as_a_copy_where_asked()
        {
            Ui.Run(() =>
            {
                GroupNode group = StartingWithAGroup();
                Assert.Equal("group1", MainViewModel.SuggestedLibraryName());
                Assert.Empty(MainViewModel.LibraryEntries());
                Assert.True(MainViewModel.SaveSelectionToLibrary("Two values"));

                LibraryEntry entry = Assert.Single(MainViewModel.LibraryEntries());
                Assert.Equal("Two values", entry.Name);
                Assert.True(MainViewModel.InLibrary("Two values"));
                Assert.Equal(Path.Combine(_folder, "library", "Two values" + MainViewModel.LibraryExtension), entry.Path);

                // Another design: the group added at the cell given, its nodes new ones, its function still f.
                MainViewModel viewModel = new();
                viewModel.NewDesign();
                MainViewModel.AddFromLibrary(entry, 40, 50);
                GroupNode added = Assert.Single(MainViewModel.Nodes.OfType<GroupNode>());
                Assert.Equal((40, 50), (added.Row, added.Column));
                Assert.True(added.IsSelected);
                Assert.NotEqual(group.ID, added.ID);
                Assert.Empty(added.Members.Select(n => n.ID).Intersect(group.Members.Select(n => n.ID)));
                Assert.Contains("function f(", Script());

                // Added again: a copy of its own, its function named anew.
                MainViewModel.History.Commit();
                MainViewModel.AddFromLibrary(entry, 40, 80);
                Assert.Equal(2, MainViewModel.Nodes.OfType<GroupNode>().Count());
                Assert.Single(MainViewModel.Nodes.OfType<GroupNode>(), g => g.IsSelected);
                string[] names = [.. MainViewModel.Nodes.OfType<GroupNode>().Select(g => g.Members.OfType<Function>().Single().Name)];
                Assert.Equal("f", names[0]);
                Assert.NotEqual("f", names[1]);
                Assert.Contains($"function {names[1]}(", Script());

                // Undone in one step.
                MainViewModel.History.Commit();
                ((ICommand)viewModel.Undo).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(MainViewModel.Nodes.OfType<GroupNode>());
            });
        }

        [Fact]
        public void Several_nodes_keep_their_wires_and_places_and_land_inside_the_canvas()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                foreach (Node node in MainViewModel.Nodes)
                {
                    node.IsSelected = true;
                }

                Assert.Equal("nodes", MainViewModel.SuggestedLibraryName());
                int wires = MainViewModel.Connections.Count;
                (int top, int left) = (MainViewModel.Nodes.Min(n => n.Row), MainViewModel.Nodes.Min(n => n.Column));
                (int Row, int Column)[] places = [.. MainViewModel.Nodes.Select(n => (n.Row - top, n.Column - left))];
                string script = Script();
                Assert.True(MainViewModel.SaveSelectionToLibrary("starting"));

                new MainViewModel().NewDesign();
                MainViewModel.AddFromLibrary(MainViewModel.LibraryEntries().Single(), 10, 20);
                Assert.Equal(wires, MainViewModel.Connections.Count);
                Assert.Equal(places, MainViewModel.Nodes.Select(n => (n.Row - 10, n.Column - 20)));
                Assert.Equal(script, Script());

                // Asked for past the canvas's edge: as far as it goes.
                MainViewModel.SelectOnly(null);
                MainViewModel.Nodes.Clear();
                MainViewModel.Connections.Clear();
                MainViewModel.AddFromLibrary(MainViewModel.LibraryEntries().Single(), Constants.ROWS + 50, -30);
                Assert.Equal(Constants.ROWS - 1, MainViewModel.Nodes.Max(n => n.Row));
                Assert.Equal(0, MainViewModel.Nodes.Min(n => n.Column));
            });
        }

        [Fact]
        public void Entries_are_named_as_files_can_be_renamed_replaced_and_deleted()
        {
            Ui.Run(() =>
            {
                StartingWithAGroup();
                Assert.Equal("a_b_c", MainViewModel.LibraryName(" a/b:c. "));
                Assert.False(MainViewModel.SaveSelectionToLibrary("  "));
                Assert.True(MainViewModel.SaveSelectionToLibrary("beta"));
                Assert.True(MainViewModel.SaveSelectionToLibrary("Alpha"));
                Assert.Equal(["Alpha", "beta"], MainViewModel.LibraryEntries().Select(e => e.Name));

                // Renamed onto another: it replaces it.
                MainViewModel.RenameInLibrary(MainViewModel.LibraryEntries()[0], "gamma");
                Assert.Equal(["beta", "gamma"], MainViewModel.LibraryEntries().Select(e => e.Name));
                MainViewModel.RenameInLibrary(MainViewModel.LibraryEntries()[1], "beta");
                Assert.Equal(["beta"], MainViewModel.LibraryEntries().Select(e => e.Name));

                MainViewModel.DeleteFromLibrary(MainViewModel.LibraryEntries()[0]);
                Assert.Empty(MainViewModel.LibraryEntries());

                // Nothing selected: nothing to save.
                MainViewModel.SelectOnly(null);
                Assert.Null(MainViewModel.SuggestedLibraryName());
                Assert.False(MainViewModel.SaveSelectionToLibrary("empty"));
            });
        }

        [Fact]
        public void Save_to_library_asks_for_a_name_and_add_node_offers_the_entries()
        {
            Ui.Run(() =>
            {
                GroupNode group = StartingWithAGroup();
                MainViewModel viewModel = new();
                string? suggested = null;
                viewModel.AskLibraryName = name => { suggested = name; return Task.FromResult<string?>("smoothing"); };
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();

                viewModel.SaveToLibrary.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("group1", suggested);
                Assert.Equal(["smoothing"], MainViewModel.LibraryEntries().Select(e => e.Name));

                // Add node's menu: the entry, then saving and managing.
                DropDownButton add = window.GetVisualDescendants().OfType<DropDownButton>().Single(b => b.Name == "AddNodeButton");
                MenuFlyout flyout = (MenuFlyout)add.Flyout!;
                flyout.ShowAt(add);
                Dispatcher.UIThread.RunJobs();
                MenuItem library = flyout.Items.OfType<MenuItem>().Single(i => i.Name == "LibraryFlyoutMenu");
                Assert.Equal(["smoothing", "Save the selection to the library…", "Manage library…"],
                    library.Items.OfType<MenuItem>().Select(i => i.Header as string));
                flyout.Hide();

                // Clicked: a copy of the group, near the middle of the view.
                library.Items.OfType<MenuItem>().First().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, MainViewModel.Nodes.OfType<GroupNode>().Count());
                Assert.Contains(MainViewModel.Nodes.OfType<GroupNode>(), g => g != group && g.IsSelected);
                window.Close();
            });
        }
    }
}
