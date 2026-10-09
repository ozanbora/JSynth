using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using JuliaSynth.ViewModels;
using JuliaSynth.Views;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Mouse input on the canvas, sent to a headless window.
    /// </summary>
    public class CanvasInputTests
    {
        [Fact]
        public void Ctrl_and_the_wheel_zoom_the_canvas()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();

                window.MouseWheel(new Point(500, 300), new Vector(0, -1), RawInputModifiers.Control);
                Assert.Equal(0.9, viewModel.SelectedZoomLevel);

                window.MouseWheel(new Point(500, 300), new Vector(0, 1), RawInputModifiers.Control);
                window.MouseWheel(new Point(500, 300), new Vector(0, 1), RawInputModifiers.Control);
                Assert.Equal(1.1, viewModel.SelectedZoomLevel);

                // Without Ctrl the wheel scrolls; the zoom stays.
                window.MouseWheel(new Point(500, 300), new Vector(0, -1), RawInputModifiers.None);
                Assert.Equal(1.1, viewModel.SelectedZoomLevel);
                window.Close();
            });
        }

        [Fact]
        public void At_100_percent_the_nodes_are_drawn_at_three_quarters_of_their_size()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();

                Assert.Equal((1.0, 0.75), (viewModel.SelectedZoomLevel, viewModel.Scale));
                Avalonia.Media.ScaleTransform scale = (Avalonia.Media.ScaleTransform)window.GetVisualDescendants().OfType<LayoutTransformControl>().First().LayoutTransform!;
                Assert.Equal(0.75, scale.ScaleX);

                // 400% at the most: three times their size.
                viewModel.SelectedZoomLevel = viewModel.MaxZoom;
                Assert.Equal((4.0, 3.0), (viewModel.SelectedZoomLevel, scale.ScaleX));
                window.Close();
            });
        }

        [Fact]
        public void Ctrl_and_dragging_the_empty_canvas_pans_the_view()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new() { ScrollViewerOffset = new Vector(300, 300) };
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();

                // Far from the starting design's nodes, and from the map in the corner.
                window.MouseDown(new Point(800, 200), MouseButton.Left);
                window.MouseMove(new Point(750, 140), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(750, 140), MouseButton.Left);

                // Dragging left and up shows more of the right and the bottom.
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);

                // With Ctrl, a drag on the empty canvas selects (a box) and doesn't pan.
                window.MouseDown(new Point(800, 200), MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(new Point(750, 140), RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(750, 140), MouseButton.Left, RawInputModifiers.Control);
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);

                // Past the top left edge: the view stops there, and dragging back moves it straight away.
                window.MouseDown(new Point(300, 150), MouseButton.Left);
                window.MouseMove(new Point(800, 600), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(800, 600), MouseButton.Left);
                Assert.Equal(new Vector(0, 0), viewModel.ScrollViewerOffset);
                window.MouseDown(new Point(800, 200), MouseButton.Left);
                window.MouseMove(new Point(750, 140), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(750, 140), MouseButton.Left);
                Assert.Equal(new Vector(50, 60), viewModel.ScrollViewerOffset);
                window.Close();
            });
        }

        [Fact]
        public void Ctrl_and_dragging_a_box_selects_the_nodes_in_it_and_a_click_on_the_empty_canvas_clears_them()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                MainViewModel.SelectOnly(null);

                // A box around the first node.
                Controls.Node node = window.GetVisualDescendants().OfType<Controls.Node>().First(n => n.IsVisible && n.Bounds.Width > 0);
                Point topLeft = node.TranslatePoint(new Point(-10, -10), window)!.Value;
                Point bottomRight = node.TranslatePoint(new Point(node.Bounds.Width + 10, node.Bounds.Height + 10), window)!.Value;
                Vector offset = viewModel.ScrollViewerOffset;
                window.MouseDown(topLeft, MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(bottomRight, RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
                window.MouseUp(bottomRight, MouseButton.Left, RawInputModifiers.Control);
                Assert.True(((Model.Node)node.DataContext!).IsSelected);
                Assert.Equal(offset, viewModel.ScrollViewerOffset);

                // A plain click there, without a drag: nothing selected, nothing moved.
                window.MouseDown(topLeft, MouseButton.Left);
                window.MouseUp(topLeft, MouseButton.Left);
                Assert.DoesNotContain(MainViewModel.Nodes, n => n.IsSelected);
                Assert.Equal(offset, viewModel.ScrollViewerOffset);
                window.Close();
            });
        }

        [Fact]
        public void A_click_on_a_wire_selects_it_and_delete_removes_it()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                MainViewModel.SelectOnly(MainViewModel.Nodes[0]);

                // The wire from x into f: halfway along, the curve is halfway between its ends.
                Model.Function f = MainViewModel.Nodes.OfType<Model.Function>().Single(n => n.Name == "f");
                Model.Connection wire = MainViewModel.ConnectionTo(f.Parameters[0])!;
                Controls.Connector[] ports = [.. window.GetVisualDescendants().OfType<Controls.Connector>()];
                Point from = Center(ports.Single(c => !c.IsInput && c.Owner?.ID == wire.SourceID && c.Output is null));
                Point to = Center(ports.Single(c => c.Parameter == f.Parameters[0]));
                Point middle = new((from.X + to.X) / 2, (from.Y + to.Y) / 2);
                Vector offset = viewModel.ScrollViewerOffset;

                // Even a few pixels off it: the wire selected, the node no longer, and the canvas not panned.
                window.MouseDown(middle + new Point(0, 3), MouseButton.Left);
                window.MouseUp(middle + new Point(0, 3), MouseButton.Left);
                Assert.Same(wire, MainViewModel.SelectedConnection);
                Assert.DoesNotContain(MainViewModel.Nodes, n => n.IsSelected);
                Assert.Equal(offset, viewModel.ScrollViewerOffset);

                // Selecting a node unselects the wire; selecting the wire again and deleting removes it, not its nodes.
                MainViewModel.SelectOnly(f);
                Assert.Null(MainViewModel.SelectedConnection);
                window.MouseDown(middle, MouseButton.Left);
                window.MouseUp(middle, MouseButton.Left);
                int nodes = MainViewModel.Nodes.Count;
                ((System.Windows.Input.ICommand)viewModel.DeleteSelected).Execute(null);
                Assert.DoesNotContain(wire, MainViewModel.Connections);
                Assert.Equal(nodes, MainViewModel.Nodes.Count);
                Assert.Null(MainViewModel.SelectedConnection);
                window.Close();

                Point Center(Controls.Connector port) => port.CenterIn(window);
            });
        }

        [Fact]
        public void The_pointer_over_a_wire_shows_what_it_carries()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                Controls.DesignerGrid grid = window.GetVisualDescendants().OfType<Controls.DesignerGrid>().Single();

                Model.Function f = MainViewModel.Nodes.OfType<Model.Function>().Single(n => n.Name == "f");
                Model.Connection wire = MainViewModel.ConnectionTo(f.Parameters[0])!;
                Model.Node x = MainViewModel.Nodes.Single(n => n.ID == wire.SourceID);
                Controls.Connector[] ports = [.. window.GetVisualDescendants().OfType<Controls.Connector>()];
                Point from = ports.Single(c => !c.IsInput && c.Owner == x && c.Output is null).CenterIn(window);
                Point to = ports.Single(c => c.Parameter == f.Parameters[0]).CenterIn(window);
                Point middle = new((from.X + to.X) / 2, (from.Y + to.Y) / 2);

                // Before a run: its type.
                window.MouseMove(middle);
                Assert.True(ToolTip.GetIsOpen(grid));
                Assert.Equal("Float64\nRun the design (F5) to see the value", ((TextBlock)ToolTip.GetTip(grid)!).Text);

                // After one: the value, its first lines.
                x.Result = new Runtime.Report(x.ID, Runtime.ReportStatus.Ok, "Matrix{Float64}", string.Join('\n', Enumerable.Range(1, 40)));
                window.MouseMove(new Point(800, 200));
                Assert.False(ToolTip.GetIsOpen(grid));
                window.MouseMove(middle);
                string[] shown = ((TextBlock)ToolTip.GetTip(grid)!).Text!.Split('\n');
                Assert.Equal(["Matrix{Float64}", "1", "2"], shown.Take(3));
                Assert.Equal("⋮", shown[^1]);
                window.Close();
            });
        }

        [Fact]
        public void A_click_on_the_map_brings_that_part_of_the_design_into_view_and_the_map_can_be_hidden()
        {
            string settingsBefore = AppSettings.SettingsPath;
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "JuliaSynth.Tests", System.Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(folder);
            AppSettings.SettingsPath = System.IO.Path.Combine(folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    // A node far down and to the right of the others.
                    MainViewModel.LoadStartingDesign();
                    Model.Float far = new() { Row = 150, Column = 200 };
                    MainViewModel.Nodes.Add(far);
                    MainViewModel viewModel = new();
                    Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                    window.Show();
                    Controls.Minimap map = window.GetVisualDescendants().OfType<Controls.Minimap>().Single();
                    Assert.True(map.IsEffectivelyVisible);

                    // A click on it on the map: it comes into the middle of the view.
                    (Rect area, double factor, Point origin) = map.Map();
                    Point farOnCanvas = new((far.Column + 0.5) * Constants.WIDTH, (far.Row + 0.5) * Constants.HEIGHT);
                    Point farOnMap = new(origin.X + ((farOnCanvas.X - area.X) * factor), origin.Y + ((farOnCanvas.Y - area.Y) * factor));
                    window.MouseDown(map.TranslatePoint(farOnMap, window)!.Value, MouseButton.Left);
                    window.MouseUp(map.TranslatePoint(farOnMap, window)!.Value, MouseButton.Left);
                    Avalonia.Controls.ScrollViewer scroll = window.GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().Single(s => s.Name == "CanvasScroll");
                    Point middle = new((viewModel.ScrollViewerOffset.X + (scroll.Viewport.Width / 2)) / viewModel.Scale, (viewModel.ScrollViewerOffset.Y + (scroll.Viewport.Height / 2)) / viewModel.Scale);
                    Assert.InRange(middle.X - farOnCanvas.X, -2 / factor, 2 / factor);
                    Assert.InRange(middle.Y - farOnCanvas.Y, -2 / factor, 2 / factor);

                    // Hidden, and kept hidden.
                    viewModel.ShowMinimap = false;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.False(map.IsEffectivelyVisible);
                    Assert.False(AppSettings.Load().ShowMinimap);
                    Assert.False(new MainViewModel().ShowMinimap);
                    window.Close();
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
                System.IO.Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void Dragging_the_top_edge_of_the_output_resizes_it()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new() { IsOutputVisible = true };
                Window window = new() { Content = new MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                Border output = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "OutputPanel");
                Border grip = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "OutputGrip");
                double before = output.Bounds.Height;
                Point edge = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, 3), window)!.Value;

                // Up 100: taller by 100.
                window.MouseDown(edge, MouseButton.Left);
                window.MouseMove(edge - new Point(0, 100), RawInputModifiers.LeftMouseButton);
                window.MouseUp(edge - new Point(0, 100), MouseButton.Left);
                window.UpdateLayout();
                Assert.Equal(before + 100, output.Bounds.Height, 1);

                // Never so short its title and lines can't show, nor so tall it hides the canvas.
                Point top = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, 3), window)!.Value;
                window.MouseDown(top, MouseButton.Left);
                window.MouseMove(top + new Point(0, 600), RawInputModifiers.LeftMouseButton);
                window.MouseUp(top + new Point(0, 600), MouseButton.Left);
                window.UpdateLayout();
                Assert.Equal(80, output.Bounds.Height, 1);
                window.Close();
            });
        }
    }
}
