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
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Frames: titled boxes drawn around nodes, moved with them, and no part of what the design works out.
    /// </summary>
    public sealed class FrameTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public FrameTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static (Node X, Node Z, Node N, Function F, Function G) StartingDesign()
        {
            MainViewModel.LoadStartingDesign();
            Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
            Function g = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g");
            Node x = MainViewModel.Nodes.OfType<Float>().Single();
            Node z = MainViewModel.Nodes.OfType<Integer>().Single();
            Node n = MainViewModel.Nodes.OfType<UnsignedInteger>().Single();

            // Room above, for a frame's title.
            foreach (Node node in MainViewModel.Nodes)
            {
                node.Row += 10;
                node.Column += 10;
            }

            return (x, z, n, f, g);
        }

        [Fact]
        public void A_frame_goes_around_the_selected_nodes_and_changes_nothing_worked_out()
        {
            Ui.Run(() =>
            {
                (Node x, Node z, Node n, Function f, _) = StartingDesign();
                string before = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;
                x.Result = new Report(x.ID, ReportStatus.Ok, "Float64", "1.5");

                MainViewModel.SelectOnly(x);
                z.IsSelected = f.IsSelected = true;
                FrameNode frame = (FrameNode)MainViewModel.AddNode(nameof(FrameNode), 0, 0);

                Assert.True(frame.IsSelected);
                Assert.All(new[] { x, z, f }, node => Assert.True(frame.Holds(node)));
                Assert.False(frame.Holds(n));
                Assert.Equal(before, ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text);

                // Named and noted: the results of the last run stay.
                frame.Title = "The power";
                frame.Note = "x to the z-th";
                Assert.NotNull(x.Result);

                // A note gets room above the nodes when the frame is drawn around them again.
                int top = frame.Row;
                frame.Fit([x, z, f]);
                Assert.Equal(top - frame.NoteRows(frame.Columns), frame.Row);
                Assert.All(new[] { x, z, f }, node => Assert.True(frame.Holds(node)));
            });
        }

        [Fact]
        public void A_frame_is_saved_with_the_design_and_undone_like_a_node()
        {
            Ui.Run(() =>
            {
                StartingDesign();
                MainViewModel viewModel = new();
                MainViewModel.History.Commit();
                FrameNode frame = (FrameNode)MainViewModel.AddNode(nameof(FrameNode), 3, 4);
                frame.Title = "Kept";
                (frame.Columns, frame.Rows) = (20, 12);
                MainViewModel.History.Commit();

                viewModel.SaveDesign(Path.Combine(_folder, "framed.jsynth"));
                viewModel.NewDesign();
                viewModel.OpenDesign(Path.Combine(_folder, "framed.jsynth"));
                FrameNode opened = MainViewModel.Nodes.OfType<FrameNode>().Single();
                Assert.Equal(("Kept", 3, 4, 20, 12), (opened.Title, opened.Row, opened.Column, opened.Columns, opened.Rows));

                ((System.Windows.Input.ICommand)viewModel.DeleteSelected).Execute(null);
                MainViewModel.SelectOnly(opened);
                ((System.Windows.Input.ICommand)viewModel.DeleteSelected).Execute(null);
                MainViewModel.History.Commit();
                Assert.Empty(MainViewModel.Nodes.OfType<FrameNode>());
                ((System.Windows.Input.ICommand)viewModel.Undo).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(MainViewModel.Nodes.OfType<FrameNode>());
            });
        }

        [Fact]
        public void Arranging_draws_each_frame_around_the_nodes_it_held()
        {
            Ui.Run(() =>
            {
                (Node x, Node z, Node n, Function f, Function g) = StartingDesign();
                MainViewModel.SelectOnly(x);
                z.IsSelected = f.IsSelected = true;
                FrameNode frame = (FrameNode)MainViewModel.AddNode(nameof(FrameNode), 0, 0);

                // The frame and its nodes far to the right, the rest far apart: all drawn together, the frame around the
                // nodes it held where they are now.
                foreach (Node node in new Node[] { frame, x, z, f })
                {
                    node.Column += 40;
                }

                (n.Row, g.Column) = (n.Row + 60, g.Column + 80);
                int frameColumn = frame.Column;
                Layout.Arrange(MainViewModel.Nodes);
                Assert.True(frame.Column < frameColumn - 20);
                Assert.All(new[] { x, z, f }, node => Assert.True(frame.Holds(node)));
                Assert.False(frame.Holds(g) || frame.Holds(n));
            });
        }

        [Fact]
        public void Dragging_a_frames_title_moves_it_and_its_nodes_and_its_corner_resizes_it()
        {
            Ui.Run(() =>
            {
                (Node x, Node z, Node n, Function f, Function g) = StartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();
                MainViewModel.SelectOnly(x);
                z.IsSelected = true;
                FrameNode frame = (FrameNode)MainViewModel.AddNode(nameof(FrameNode), 0, 0);
                MainViewModel.SelectOnly(null);
                Dispatcher.UIThread.RunJobs();
                Views.FrameNode view = window.GetVisualDescendants().OfType<Views.FrameNode>().Single();

                // The title bar, then 120 pixels right and 90 down.
                (int Row, int Column)[] before = [.. new[] { frame, x, z, n, f, g }.Select(m => (m.Row, m.Column))];
                Point title = view.TranslatePoint(new Point(60, 16), window)!.Value;
                window.MouseDown(title, MouseButton.Left);
                window.MouseMove(title + new Point(60, 45), RawInputModifiers.LeftMouseButton);
                window.MouseMove(title + new Point(120, 90), RawInputModifiers.LeftMouseButton);
                window.MouseUp(title + new Point(120, 90), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                (int Row, int Column)[] after = [.. new[] { frame, x, z, n, f, g }.Select(m => (m.Row, m.Column))];
                (int rows, int columns) = (after[0].Row - before[0].Row, after[0].Column - before[0].Column);
                Assert.True(rows > 0 && columns > 0);
                Assert.Equal([before[1], before[2]], after[1..3].Select(p => (p.Row - rows, p.Column - columns)));
                Assert.Equal(before[3..], after[3..]);

                // The corner: wider and taller.
                (int columnsBefore, int rowsBefore) = (frame.Columns, frame.Rows);
                Dispatcher.UIThread.RunJobs();
                Point corner = view.TranslatePoint(new Point(view.Bounds.Width - 6, view.Bounds.Height - 6), window)!.Value;
                window.MouseDown(corner, MouseButton.Left);
                window.MouseMove(corner + new Point(75, 45), RawInputModifiers.LeftMouseButton);
                window.MouseUp(corner + new Point(75, 45), MouseButton.Left);
                Assert.True(frame.Columns > columnsBefore && frame.Rows > rowsBefore);
                Assert.Equal(after[1..], new[] { x, z, n, f, g }.Select(m => (m.Row, m.Column)));
                window.Close();
            });
        }

        [Fact]
        public void A_box_in_a_frame_selects_the_nodes_in_it_not_the_frame()
        {
            Ui.Run(() =>
            {
                (Node x, Node z, _, _, _) = StartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();
                MainViewModel.SelectOnly(x);
                z.IsSelected = true;
                FrameNode frame = (FrameNode)MainViewModel.AddNode(nameof(FrameNode), 0, 0);
                MainViewModel.SelectOnly(null);
                Dispatcher.UIThread.RunJobs();

                Controls.Node xView = window.GetVisualDescendants().OfType<Controls.Node>().Single(v => v.DataContext == x);
                Point from = xView.TranslatePoint(new Point(-5, -5), window)!.Value;
                Point to = xView.TranslatePoint(new Point(xView.Bounds.Width + 5, xView.Bounds.Height + 5), window)!.Value;
                window.MouseDown(from, MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(to, RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
                window.MouseUp(to, MouseButton.Left, RawInputModifiers.Control);

                Assert.True(x.IsSelected);
                Assert.False(frame.IsSelected);
                window.Close();
            });
        }
    }
}
