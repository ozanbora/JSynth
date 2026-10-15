using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using JSynth.Controls;
using JSynth.Runtime;
using JSynth.Views.Outputs;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// A matrix drawn as a 3D surface: where its points land on screen, the order its squares are drawn in, and what
    /// the mouse points at.
    /// </summary>
    public sealed class SurfaceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public SurfaceTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// A surface's plot file as Julia writes it: the header, then the values column by column.
        /// </summary>
        private PlotData Written(long shownRows, long shownColumns, long rows, long columns, double[] values)
        {
            string path = Path.Combine(_folder, $"{Guid.NewGuid():N}.jsplot");
            using (BinaryWriter writer = new(File.Create(path)))
            {
                writer.Write("JSPLOT01"u8.ToArray());
                foreach (long n in new[] { (long)PlotKind.Surface, shownRows, shownColumns, rows, columns })
                {
                    writer.Write(n);
                }

                foreach (double x in values)
                {
                    writer.Write(x);
                }
            }

            return PlotData.Read(path);
        }

        [Fact]
        public void A_point_lands_on_screen_as_the_camera_looks_at_it()
        {
            // 310 × 310 pixels: 100 pixels to the box's half width, the box's middle in the middle.
            SurfaceCamera front = new(0, 0, 1);
            Assert.Equal((255.0, 155.0, 0.0), Rounded(front.Project(1, 0, 0, 310, 310)));
            Assert.Equal((155.0, 55.0, 0.0), Rounded(front.Project(0, 0, 1, 310, 310)));
            Assert.Equal((155.0, 155.0, 1.0), Rounded(front.Project(0, -1, 0, 310, 310)));

            // Turned a quarter: y runs to the left. From above: y runs up, and the top is nearest.
            Assert.Equal((55.0, 155.0, 0.0), Rounded(new SurfaceCamera(90, 0, 1).Project(0, 1, 0, 310, 310)));
            Assert.Equal((155.0, 55.0, 0.0), Rounded(new SurfaceCamera(0, 90, 1).Project(0, 1, 0, 310, 310)));
            Assert.Equal(1.0, Math.Round(new SurfaceCamera(0, 90, 1).Project(0, 0, 1, 310, 310).Depth, 9));

            // At first, the corner where the matrix starts (x = y = -1) is nearest: the axes meet there.
            double[] corners = [.. new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }.Select(c => SurfaceCamera.Start.Project(c.Item1, c.Item2, 0, 310, 310).Depth)];
            Assert.Equal(corners.Max(), corners[0]);

            // Zoomed in twice: twice as far from the middle.
            Assert.Equal((355.0, 155.0, 0.0), Rounded(new SurfaceCamera(0, 0, 2).Project(1, 0, 0, 310, 310)));
        }

        [Fact]
        public void The_camera_turns_all_the_way_around_but_tilts_no_further_than_straight_above_or_below()
        {
            SurfaceCamera turned = SurfaceCamera.Start.Turned(200, 100);
            Assert.Equal((-125.0, 90.0), (Math.Round(turned.Azimuth, 9), turned.Elevation));
            Assert.Equal(-90, turned.Turned(0, -500).Elevation);
            Assert.Equal(8, SurfaceCamera.Start.Zoomed(100).Zoom);
            Assert.Equal(0.25, SurfaceCamera.Start.Zoomed(0.001).Zoom);
        }

        [Fact]
        public void Squares_are_drawn_from_the_back_to_the_front()
        {
            // 3 rows, 2 columns: two squares, row 1's in front (y = -1) unturned.
            SurfaceMesh mesh = new(Written(3, 2, 3, 2, [1, 2, 3, 4, 5, 6]));

            Assert.Equal([(1, 0), (0, 0)], mesh.Order(mesh.Project(new SurfaceCamera(0, 30, 1), 300, 300)));
            Assert.Equal([(0, 0), (1, 0)], mesh.Order(mesh.Project(new SurfaceCamera(180, 30, 1), 300, 300)));
        }

        [Fact]
        public void Squares_with_a_value_that_isnt_finite_are_left_out()
        {
            SurfaceMesh mesh = new(Written(3, 2, 3, 2, [1, 2, double.NaN, 4, 5, 6]));

            // Row 3, column 1: the second square only.
            Assert.Equal([(0, 0)], mesh.Order(mesh.Project(new SurfaceCamera(0, 30, 1), 300, 300)));
            Assert.Equal((1.0, 6.0), (mesh.Min, mesh.Max));
        }

        [Fact]
        public void The_mouse_points_at_the_nearest_grid_point_of_the_square_under_it()
        {
            // 1 to 9, from straight above: the grid points 100 pixels apart around the middle, row 1 at the bottom.
            SurfaceMesh mesh = new(Written(3, 3, 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]));
            (double X, double Y, double Depth)[,] points = mesh.Project(new SurfaceCamera(0, 90, 1), 310, 310);
            var order = mesh.Order(points);

            Assert.Equal((1, 1), mesh.PointAt(points, order, 157, 153));
            Assert.Equal((0, 0), mesh.PointAt(points, order, 85, 245));
            Assert.Null(mesh.PointAt(points, order, 10, 10));
            Assert.Equal("row 2, column 2: 5", mesh.Describe(1, 1));
            Assert.Equal("row 1, column 3: 7", mesh.Describe(0, 2));

            // Seen edge on, a flat surface covers nothing.
            SurfaceMesh flat = new(Written(2, 2, 2, 2, [1, 1, 1, 1]));
            (double X, double Y, double Depth)[,] edgeOn = flat.Project(new SurfaceCamera(0, 0, 1), 310, 310);
            Assert.Null(flat.PointAt(edgeOn, flat.Order(edgeOn), 155, 155));
        }

        [Fact]
        public void An_averaged_down_point_names_the_block_of_rows_and_columns_it_stands_for()
        {
            // 5 × 6 averaged in blocks of 2: 3 × 3.
            SurfaceMesh mesh = new(Written(3, 3, 5, 6, [1, 2, 3, 4, 5, 6, 7, 8, 9]));

            Assert.Equal("rows 3–4, columns 5–6: ≈ 8   (an average: the matrix is shown averaged down)", mesh.Describe(1, 2));
            Assert.Equal((5L, 5L), mesh.RowsOf(2));

            // The axes run from the first block's middle to the last's.
            Assert.Equal((-1.0, 1.0), (mesh.Y(1.5), mesh.Y(5)));
            Assert.Equal((-1.0, 1.0), (mesh.X(1.5), mesh.X(5.5)));
        }

        [Fact]
        public void Axes_are_marked_at_round_values()
        {
            Assert.Equal(new double[] { 2, 4, 6, 8, 10 }, SurfaceMesh.Ticks(1, 10, 6, whole: true));
            Assert.Equal(new double[] { 1, 2, 3 }, SurfaceMesh.Ticks(1, 3, 6, whole: true));
            Assert.Equal(new double[] { 0, 0.5, 1 }, SurfaceMesh.Ticks(0, 1, 3, whole: false));
            Assert.Equal(new double[] { 0.1, 0.2, 0.3 }, SurfaceMesh.Ticks(0.1, 0.3, 2, whole: false));
            Assert.Equal(new double[] { 7 }, SurfaceMesh.Ticks(7, 7, 6, whole: false));
            Assert.Empty(SurfaceMesh.Ticks(double.NaN, double.NaN, 6, whole: false));
        }

        [Fact]
        public void In_the_window_a_surface_turns_zooms_and_says_what_the_mouse_is_on()
        {
            // A bump: higher in the middle.
            double[] bump = new double[20 * 20];
            for (int j = 0; j < 20; j++)
            {
                for (int i = 0; i < 20; i++)
                {
                    bump[(j * 20) + i] = Math.Exp(-(Math.Pow(i - 9.5, 2) + Math.Pow(j - 9.5, 2)) / 30);
                }
            }

            PlotData data = Written(20, 20, 20, 20, bump);
            Ui.Run(() =>
            {
                Model.PlotNode model = new() { Plot = data, Title = "A bump" };
                PlotWindow window = new(model);
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.True(window.Surface.IsVisible);
                Assert.Equal(PlotReadout.SurfaceHint, window.Readout);

                using (WriteableBitmap? frame = window.CaptureRenderedFrame())
                {
                    Assert.NotNull(frame);

                    // Seen by eye once: the picture, where the test asks for it.
                    if (Environment.GetEnvironmentVariable("JSYNTH_SURFACE_PICTURE") is string picture)
                    {
                        frame.Save(picture);
                    }
                }

                // The middle of the window is on the bump.
                Point middle = window.Surface.TranslatePoint(new Point(window.Surface.Bounds.Width / 2, window.Surface.Bounds.Height / 2), window)!.Value;
                window.MouseMove(middle);
                Dispatcher.UIThread.RunJobs();
                Assert.Matches(@"^row \d+, column \d+: 0\.\d+$", window.Readout);

                // Dragging turns it; the wheel zooms; a double-click goes back to the start.
                window.MouseDown(middle, MouseButton.Left);
                window.MouseMove(middle + new Point(50, 20));
                window.MouseUp(middle + new Point(50, 20), MouseButton.Left);
                Assert.Equal((15.0, 38.0), (Math.Round(window.Surface.Camera.Azimuth, 9), Math.Round(window.Surface.Camera.Elevation, 9)));
                window.MouseWheel(middle, new Vector(0, 1));
                Assert.Equal(1.15, Math.Round(window.Surface.Camera.Zoom, 9));

                // A new value (live, in a stream) keeps the view.
                SurfaceCamera turned = window.Surface.Camera;
                model.Plot = Written(20, 20, 20, 20, bump);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(turned, window.Surface.Camera);

                window.Surface.ResetView();
                Assert.Equal(SurfaceCamera.Start, window.Surface.Camera);

                // Off the surface: the hint again.
                window.MouseMove(new Point(2, 2));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(PlotReadout.SurfaceHint, window.Readout);
                window.Close();
            });
        }

        [Fact]
        public void On_the_node_a_surface_shows_in_place_of_the_chart_too_small_to_turn()
        {
            PlotData data = Written(3, 3, 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
            Ui.Run(() =>
            {
                Model.PlotNode model = new() { Plot = data, Title = "Rising" };
                PlotNode node = new() { DataContext = model };
                Avalonia.Controls.Window window = new() { Content = node, Width = 400, Height = 320 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                SurfaceView surface = node.FindControl<SurfaceView>("Surface")!;
                ScottPlot.Avalonia.AvaPlot chart = node.FindControl<ScottPlot.Avalonia.AvaPlot>("Chart")!;
                Assert.True(surface.IsVisible);
                Assert.False(surface.IsHitTestVisible);
                Assert.False(chart.IsVisible);

                using (WriteableBitmap? frame = window.CaptureRenderedFrame())
                {
                    if (Environment.GetEnvironmentVariable("JSYNTH_SURFACE_PICTURE") is string picture)
                    {
                        frame?.Save(Path.ChangeExtension(picture, ".node.png"));
                    }
                }

                // Cleared (the design changed): neither, the message instead.
                model.Plot = null;
                Assert.False(surface.IsVisible);
                Assert.False(chart.IsVisible);
                window.Close();
            });
        }

        private static (double, double, double) Rounded((double X, double Y, double Depth) p)
        {
            return (Math.Round(p.X, 9) + 0, Math.Round(p.Y, 9) + 0, Math.Round(p.Depth, 9) + 0);
        }
    }
}
