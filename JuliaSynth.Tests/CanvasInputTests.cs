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

                // Far from the starting design's nodes.
                window.MouseDown(new Point(900, 500), MouseButton.Left);
                window.MouseMove(new Point(850, 440), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(850, 440), MouseButton.Left);

                // Dragging left and up shows more of the right and the bottom.
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);

                // With Ctrl, a drag on the empty canvas selects (a box) and doesn't pan.
                window.MouseDown(new Point(900, 500), MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(new Point(850, 440), RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(850, 440), MouseButton.Left, RawInputModifiers.Control);
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);
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
