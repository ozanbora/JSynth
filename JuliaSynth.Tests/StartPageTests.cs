using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The start page: shown when there is nothing to reopen, with the examples, and how to install Julia
    /// when it is missing.
    /// </summary>
    public sealed class StartPageTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public StartPageTests()
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
        public void It_opens_when_there_is_no_design_to_reopen_but_not_over_one()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel first = new();
                first.StartUp();
                Assert.True(first.IsStartPageOpen);
                first.EndJulia();

                string path = Path.Combine(_folder, "kept.jsynth");
                first.SaveDesign(path);
                MainViewModel next = new();
                next.StartUp();
                Assert.False(next.IsStartPageOpen);
                next.EndJulia();
            });
        }

        [Fact]
        public void Starting_anything_closes_it_and_escape_closes_it_before_clearing_the_selection()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();

                foreach (Action start in new Action[] { () => viewModel.OpenExample(Examples.All[0]), viewModel.NewDesign })
                {
                    ((System.Windows.Input.ICommand)viewModel.ShowStartPage).Execute(null);
                    Assert.True(viewModel.IsStartPageOpen);
                    start();
                    Assert.False(viewModel.IsStartPageOpen);
                }

                MainViewModel.LoadStartingDesign();
                MainViewModel.Nodes[0].IsSelected = true;
                ((System.Windows.Input.ICommand)viewModel.ShowStartPage).Execute(null);
                ((System.Windows.Input.ICommand)viewModel.ClearSelection).Execute(null);
                Assert.False(viewModel.IsStartPageOpen);
                Assert.True(MainViewModel.Nodes[0].IsSelected);
                ((System.Windows.Input.ICommand)viewModel.ClearSelection).Execute(null);
                Assert.False(MainViewModel.Nodes[0].IsSelected);
            });
        }

        [Fact]
        public void An_empty_canvas_says_how_to_start_unless_the_start_page_does()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Assert.False(viewModel.IsCanvasHintVisible);

                viewModel.NewDesign();
                Assert.True(viewModel.IsCanvasHintVisible);

                ((System.Windows.Input.ICommand)viewModel.ShowStartPage).Execute(null);
                Assert.False(viewModel.IsCanvasHintVisible);
            });
        }

        [Fact]
        public void Each_example_card_says_its_field_and_what_it_works_out()
        {
            Assert.All(Examples.All, example =>
            {
                Assert.False(string.IsNullOrWhiteSpace(example.Category));
                Assert.DoesNotContain(':', example.Category);
                Assert.EndsWith(".", example.Summary);
                Assert.False(char.IsAsciiLetterLower(example.Summary[0]), example.Summary);
                Assert.StartsWith(example.Summary[..^1].ToLowerInvariant(), example.Description.ToLowerInvariant()[(example.Category.Length + 2)..]);
            });

            Example loan = Examples.All.Single(e => e.Name == "Loan payment");
            Assert.Equal("Finance", loan.Category);
            Assert.Equal("The monthly payment on a 250,000 loan at 4.5% a year over 30 years, and the interest paid in total.", loan.Summary);
        }

        [Fact]
        public void Without_julia_it_shows_how_to_install_it_and_picks_julia_up_once_there()
        {
            Ui.Run(async () =>
            {
                // Julia stands in for one installed while the page is open.
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation installed)
                {
                    return;
                }

                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                viewModel.Julia = null;
                Assert.True(viewModel.IsJuliaMissing);
                Assert.False(viewModel.IsJuliaFound);
                Assert.Contains(OperatingSystem.IsWindows() ? "winget install" : "install.julialang.org", MainViewModel.JuliaInstallCommand);

                Stopwatch clock = Stopwatch.StartNew();
                while (viewModel.Julia is null && clock.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(100);
                }

                Assert.Equal(installed, viewModel.Julia);
                Assert.False(viewModel.IsJuliaMissing);
                Assert.Contains(viewModel.Output, o => o.Text == $"Found Julia {installed.Version} at {installed.Path}.");
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void In_the_window_a_card_opens_its_example()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Views.MainWindow window = new() { DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();
                ((System.Windows.Input.ICommand)viewModel.ShowStartPage).Execute(null);
                Dispatcher.UIThread.RunJobs();

                Button[] cards = window.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("example")).ToArray();
                Assert.Equal(Examples.All.Count, cards.Length);

                Button pageRank = cards.Single(c => c.DataContext is Example { Name: "PageRank" });
                pageRank.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();

                Assert.False(viewModel.IsStartPageOpen);
                Assert.Contains(viewModel.Output, o => o.Text.StartsWith("Example: PageRank."));
                window.Close();
            });
        }
    }
}
