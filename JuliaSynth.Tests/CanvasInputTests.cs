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
                window.MouseDown(new Point(900, 500), MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(new Point(850, 440), RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(850, 440), MouseButton.Left, RawInputModifiers.Control);

                // Dragging left and up shows more of the right and the bottom.
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);

                // Without Ctrl, a drag on the empty canvas selects (a box) and doesn't pan.
                window.MouseDown(new Point(900, 500), MouseButton.Left, RawInputModifiers.None);
                window.MouseMove(new Point(850, 440), RawInputModifiers.LeftMouseButton);
                window.MouseUp(new Point(850, 440), MouseButton.Left, RawInputModifiers.None);
                Assert.Equal(new Vector(350, 360), viewModel.ScrollViewerOffset);
                window.Close();
            });
        }
    }
}
