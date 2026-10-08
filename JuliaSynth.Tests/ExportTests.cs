using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

using Avalonia.Threading;
using Avalonia.VisualTree;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Taking a design elsewhere: its selected nodes as Julia to paste, and the design as a picture.
    /// </summary>
    public sealed class ExportTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public ExportTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void Copy_as_julia_takes_the_selected_nodes_and_all_they_need_and_runs_on_its_own()
        {
            Ui.Run(async () =>
            {
                // The starting design: f(x, z), then g(f, n).
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                string? copied = null;
                viewModel.SetClipboardText = text => { copied = text; return System.Threading.Tasks.Task.CompletedTask; };
                Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");

                // f: its definition and its inputs, nothing after it.
                MainViewModel.SelectOnly(f);
                ((System.Windows.Input.ICommand)viewModel.CopyAsJulia).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(copied);
                Assert.Contains("function f(", copied);
                Assert.DoesNotContain("function g(", copied);
                Assert.Contains("x ^ z", copied);
                Assert.True(f.IsSelected);

                // Nothing selected: the whole design.
                MainViewModel.SelectOnly(null);
                Assert.Equal(MainViewModel.ExportScript(), MainViewModel.JuliaOfSelection());

                if (await JuliaLocator.FindAsync(null) is JuliaInstallation julia)
                {
                    using JuliaSession session = new(julia.Path);
                    ConcurrentQueue<OutputLine> output = new();
                    RunResult result = await session.RunAsync(copied, _folder, output.Enqueue);
                    Assert.True(result.Succeeded, string.Join("\n", output.Select(l => l.Text)));
                }
            });
        }

        [Fact]
        public void The_picture_is_the_design_at_twice_its_size_without_grid_lines_or_selection()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                Controls.DesignerGrid grid = window.GetVisualDescendants().OfType<Controls.DesignerGrid>().Single();

                // Zoomed out on the canvas, the picture is still drawn at its full size.
                viewModel.SelectedZoomLevel = 0.5;
                MainViewModel.SelectOnly(MainViewModel.Nodes[0]);
                Dispatcher.UIThread.RunJobs();
                using RenderTargetBitmap picture = grid.Picture()!;
                Rect drawn = grid.DrawnBounds()!.Value;
                Assert.Equal(new PixelSize((int)Math.Ceiling((drawn.Width + 48) * 2), (int)Math.Ceiling((drawn.Height + 48) * 2)), picture.PixelSize);
                Assert.True(MainViewModel.Nodes[0].IsSelected);

                // The margin above the nodes is the plain background, but for the faint edge of their shadows: no grid
                // lines, which are clearly darker.
                int width = picture.PixelSize.Width;
                byte[] pixels = new byte[width * 4 * 40];
                GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    picture.CopyPixels(new PixelRect(0, 0, width, 40), handle.AddrOfPinnedObject(), pixels.Length, width * 4);
                }
                finally
                {
                    handle.Free();
                }

                byte[][] colours = [.. pixels.Chunk(4)];
                Assert.All(colours, p => Assert.InRange(Math.Abs(p[0] - colours[0][0]) + Math.Abs(p[1] - colours[0][1]) + Math.Abs(p[2] - colours[0][2]), 0, 12));

                // Nothing on the canvas: nothing to draw.
                MainViewModel.Nodes.Clear();
                Dispatcher.UIThread.RunJobs();
                Assert.Null(grid.Picture());
                window.Close();
            });
        }
    }
}
