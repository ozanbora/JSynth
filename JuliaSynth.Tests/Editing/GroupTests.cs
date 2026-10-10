using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace JuliaSynth.Tests.Editing
{
    /// <summary>
    /// Groups: nodes made one, the wires coming in its inputs and those going out its outputs; working out what they did.
    /// </summary>
    public sealed class GroupTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public GroupTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static string Script(bool instrumented) =>
            ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented).Text;

        /// <summary>
        /// A script's lines in order: values not depending on each other can come in either order, and so can the
        /// nodes a value waits on.
        /// </summary>
        private static string[] Lines(string script) => [.. script.Split('\n')
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"_jsynth_blocked\(([^)]*)\)",
                m => $"_jsynth_blocked({string.Join(", ", m.Groups[1].Value.Split(", ").Order(StringComparer.Ordinal))})"))
            .Order(StringComparer.Ordinal)];

        /// <summary>
        /// The starting design, f(x, z) then g(f, n), with z and f selected.
        /// </summary>
        private static (Node X, Node Z, Function F, Function G) StartingWithZAndFSelected()
        {
            MainViewModel.LoadStartingDesign();
            Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
            Function g = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g");
            Node x = MainViewModel.Nodes.OfType<Float>().Single();
            Node z = MainViewModel.Nodes.OfType<Integer>().Single();
            MainViewModel.SelectOnly(z);
            f.IsSelected = true;
            return (x, z, f, g);
        }

        private static bool Overlap(Node a, Node b)
        {
            (double aTop, double aLeft, double aBottom, double aRight) = Layout.Footprint(a);
            (double bTop, double bLeft, double bBottom, double bRight) = Layout.Footprint(b);
            return aLeft < bRight && bLeft < aRight && aTop < bBottom && bTop < aBottom;
        }

        [Fact]
        public void A_group_takes_the_wires_coming_in_as_inputs_and_going_out_as_outputs_and_works_out_the_same()
        {
            Ui.Run(() =>
            {
                (Node x, Node z, Function f, Function g) = StartingWithZAndFSelected();
                string[] wiresBefore = [.. MainViewModel.Connections.Select(c => c.Key).Order()];
                (string plain, string run) = (Script(false), Script(true));

                GroupNode group = MainViewModel.GroupSelection()!;
                Assert.Equal("group1", group.Name);
                Assert.Equal([z, f], group.Members);
                Assert.Equal(["x::Float64"], group.Inputs.Select(p => p.Signature));
                Assert.Equal(["f"], group.Exits.Select(e => e.Name));
                Assert.Equal("Float64", group.OutputType);
                Assert.DoesNotContain(MainViewModel.Nodes, n => n == z || n == f);
                Assert.Contains(MainViewModel.Connections, c => c.SourceID == x.ID && c.TargetID == group.ID);
                Assert.Contains(MainViewModel.Connections, c => c.SourceID == group.ID && c.TargetID == g.ID && c.Output is null);
                Assert.True(group.IsSelected);

                // The same script, plain and for a run: its values but in another order.
                Assert.Equal(Lines(plain), Lines(Script(false)));
                Assert.Equal(Lines(run), Lines(Script(true)));

                // Opened: the nodes and wires as they were, in a frame named after it.
                (int rows, int columns) = (f.Row - z.Row, f.Column - z.Column);
                MainViewModel.OpenGroup(group);
                Assert.Equal(wiresBefore, MainViewModel.Connections.Select(c => c.Key).Order());
                Assert.DoesNotContain(MainViewModel.Nodes, n => n is GroupNode);
                Assert.Equal((rows, columns), (f.Row - z.Row, f.Column - z.Column));
                FrameNode frame = Assert.Single(MainViewModel.Nodes.OfType<FrameNode>());
                Assert.Equal("group1", frame.Title);
                Assert.True(frame.Holds(z) && frame.Holds(f));

                // The frame is bigger than the group was: the nodes around it made room, so it's drawn over none of them.
                Assert.False(frame.Holds(x) || frame.Holds(g));
                Assert.Empty(frame.Outside);
                Assert.All([x, g], n => Assert.False(Overlap(frame, n)));

                // The frame selected alone, grouped again: named after it, its nodes those it had, not x.
                MainViewModel.SelectOnly(frame);
                GroupNode again = MainViewModel.GroupSelection()!;
                Assert.Equal("group1", again.Name);
                Assert.Equal(new[] { f.ID, z.ID }.Order(), again.Members.Select(n => n.ID).Order());
                Assert.Contains(x, MainViewModel.Nodes);
                Assert.Empty(MainViewModel.Nodes.OfType<FrameNode>());
                Assert.Equal(Lines(plain), Lines(Script(false)));
            });
        }

        [Fact]
        public void A_group_is_saved_undone_and_pasted_as_a_copy_of_its_own()
        {
            Ui.Run(() =>
            {
                StartingWithZAndFSelected();
                MainViewModel viewModel = new();
                MainViewModel.History.Commit();
                GroupNode group = MainViewModel.GroupSelection()!;
                MainViewModel.History.Commit();
                string plain = Script(false);

                viewModel.SaveDesign(Path.Combine(_folder, "grouped.jsynth"));
                viewModel.NewDesign();
                viewModel.OpenDesign(Path.Combine(_folder, "grouped.jsynth"));
                Assert.Equal(Lines(plain), Lines(Script(false)));

                // Pasted: its nodes copies of their own, its function named anew (its input not wired: x wasn't copied).
                MainViewModel.SelectOnly(MainViewModel.Nodes.OfType<GroupNode>().Single());
                Assert.True(MainViewModel.CopySelection());
                MainViewModel.PasteClipboard();
                GroupNode[] groups = [.. MainViewModel.Nodes.OfType<GroupNode>()];
                Assert.Equal(2, groups.Length);
                Assert.Empty(groups[0].Members.Select(n => n.ID).Intersect(groups[1].Members.Select(n => n.ID)));
                Assert.NotEqual(groups[0].Members.OfType<Function>().Single().Name, groups[1].Members.OfType<Function>().Single().Name);
                string pasted = Script(false);
                Assert.Contains("function f(", pasted);
                Assert.Contains("function h(", pasted);
                Assert.Contains("# h_out: Parameter x isn't wired.", pasted);

                // Undone, back to before the paste.
                MainViewModel.History.Commit();
                ((ICommand)viewModel.Undo).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(MainViewModel.Nodes.OfType<GroupNode>());
            });
        }

        [Fact]
        public void Double_clicking_a_group_opens_it()
        {
            Ui.Run(() =>
            {
                StartingWithZAndFSelected();
                MainViewModel viewModel = new();
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();
                GroupNode group = MainViewModel.GroupSelection()!;
                Dispatcher.UIThread.RunJobs();

                Views.GroupNode view = window.GetVisualDescendants().OfType<Views.GroupNode>().Single();
                Point title = view.TranslatePoint(new Point(view.Bounds.Width / 2, 60), window)!.Value;
                window.MouseDown(title, MouseButton.Left);
                window.MouseUp(title, MouseButton.Left);
                window.MouseDown(title, MouseButton.Left);
                window.MouseUp(title, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                Assert.DoesNotContain(group, MainViewModel.Nodes);
                Assert.Single(MainViewModel.Nodes.OfType<FrameNode>());

                // The frame's Group button: one node again, named after it.
                Button button = window.GetVisualDescendants().OfType<Views.FrameNode>().Single()
                    .GetVisualDescendants().OfType<Button>().Single(b => b.Name == "GroupButton");
                button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                GroupNode again = Assert.Single(MainViewModel.Nodes.OfType<GroupNode>());
                Assert.Equal(group.Name, again.Name);
                Assert.Empty(MainViewModel.Nodes.OfType<FrameNode>());

                // The group's Open button: its frame again.
                Button open = window.GetVisualDescendants().OfType<Views.GroupNode>().Single()
                    .GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OpenButton");
                open.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(MainViewModel.Nodes.OfType<GroupNode>());

                // The frame's title double-clicked (left of its buttons): one node again.
                Views.FrameNode frameView = window.GetVisualDescendants().OfType<Views.FrameNode>().Single();
                Point frameTitle = frameView.TranslatePoint(new Point(40, 17), window)!.Value;
                window.MouseDown(frameTitle, MouseButton.Left);
                window.MouseUp(frameTitle, MouseButton.Left);
                window.MouseDown(frameTitle, MouseButton.Left);
                window.MouseUp(frameTitle, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(group.Name, Assert.Single(MainViewModel.Nodes.OfType<GroupNode>()).Name);
                Assert.Empty(MainViewModel.Nodes.OfType<FrameNode>());
                window.Close();
            });
        }

        [Fact]
        public void A_group_opened_in_a_frame_makes_room_for_its_nodes_instead_of_lying_on_its_neighbours()
        {
            Ui.Run(() =>
            {
                // The Earthquake detector's background group, between energy and ratio in the frame "How hard each second shook".
                MainViewModel viewModel = new();
                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Earthquake detector"));
                GroupNode group = MainViewModel.Nodes.OfType<GroupNode>().Single(g => g.Name == "background");
                FrameNode around = MainViewModel.Nodes.OfType<FrameNode>().Single(f => f.Holds(group));
                Node[] neighbours = [.. MainViewModel.Nodes.Where(n => n != group && n is not FrameNode)];
                Node[] heldBefore = [.. neighbours.Where(around.Holds)];
                (FrameNode Frame, int Rows, int Columns)[] otherFrames = [.. MainViewModel.Nodes.OfType<FrameNode>()
                    .Where(f => f != around).Select(f => (f, f.Rows, f.Columns))];
                Assert.Contains(heldBefore, n => n is Function { Name: "energy" });

                MainViewModel.OpenGroup(group);
                FrameNode frame = MainViewModel.Nodes.OfType<FrameNode>().Single(f => f.Title == "background");

                // Its frame lies on none of the nodes that weren't in the group; the frame around still holds them all.
                Assert.All(neighbours, n => Assert.False(Overlap(frame, n), $"{n.Variable} is under the opened group"));
                Assert.Empty(frame.Outside);
                Assert.All([.. heldBefore, .. group.Members, frame], n => Assert.True(around.Holds(n), $"{n.Variable} left its frame"));

                // The bigger frame's middle is in the opened one, but the opened one doesn't hold it: dragged, it goes alone.
                Assert.False(frame.Holds(around));

                // Only the frame around grew: the others are as big as they were, wherever they had to go.
                Assert.All(otherFrames, o => Assert.Equal((o.Rows, o.Columns), (o.Frame.Rows, o.Frame.Columns)));
                Node[] all = [.. MainViewModel.Nodes.Where(n => n is not FrameNode)];
                Assert.All(all, a => Assert.DoesNotContain(all, b => a != b && Overlap(a, b)));
            });
        }

        [Fact]
        public void Real_julia_works_out_a_group_in_a_stream_carrying_a_value_through_it()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // Live Monte Carlo π, its darts, total and estimate made one, ticking fast.
                MainViewModel viewModel = new() { Julia = julia };
                try
                {
                    viewModel.OpenExample(Examples.All.Single(e => e.Name == "Live Monte Carlo π"));
                    EveryNode clock = MainViewModel.Nodes.OfType<EveryNode>().Single();
                    (clock.Seconds, clock.Limit) = (0.01, 50);
                    MainViewModel.SelectOnly(null);
                    foreach (Function f in MainViewModel.Nodes.OfType<Function>().Where(f => f.Name is "darts" or "total" or "estimate"))
                    {
                        f.IsSelected = true;
                    }

                    GroupNode group = MainViewModel.GroupSelection()!;
                    Assert.Equal(["tick", "n", "hits"], group.Inputs.Select(p => p.Name));
                    Assert.Equal(["estimate", "total"], group.Exits.Select(e => e.Name));

                    await viewModel.RunDesignAsync();
                    Dispatcher.UIThread.RunJobs();
                    string log = string.Join("\n", viewModel.Output.Select(o => o.Text));
                    Function estimate = group.Members.OfType<Function>().Single(f => f.Name == "estimate");
                    Assert.InRange(double.Parse(estimate.Result?.Text ?? "0", System.Globalization.CultureInfo.InvariantCulture), Math.PI - 0.02, Math.PI + 0.02);
                    Assert.Equal(ReportStatus.Ok, group.Result?.Status);
                    Assert.StartsWith("tick 50,", clock.Result?.Text ?? log, StringComparison.Ordinal);
                }
                finally
                {
                    viewModel.EndJulia();
                }
            });
        }
    }
}
