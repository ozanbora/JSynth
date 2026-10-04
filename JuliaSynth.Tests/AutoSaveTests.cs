using Avalonia.Threading;
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
    /// Autosave: a moment after a change, a design file is saved, and a design never saved is kept to
    /// open again at the next start.
    /// </summary>
    public sealed class AutoSaveTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;
        private readonly TimeSpan _originalDelay = MainViewModel.AutoSaveDelay;

        public AutoSaveTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            MainViewModel.AutoSaveDelay = TimeSpan.FromMilliseconds(50);
        }

        public void Dispose()
        {
            MainViewModel.StopAutoSave();
            MainViewModel.AutoSaveDelay = _originalDelay;
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static async Task<bool> SavedAsync(MainViewModel viewModel)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (viewModel.IsDirty && clock.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();
            }

            return !viewModel.IsDirty;
        }

        [Fact]
        public void A_design_file_saves_itself_after_a_change()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                viewModel.StartUp();
                Assert.True(viewModel.AutoSave);
                string path = Path.Combine(_folder, "design.jsynth");
                viewModel.SaveDesign(path);
                string before = File.ReadAllText(path);

                MainViewModel.Nodes[0].Row += 3;
                Assert.True(viewModel.IsDirty);
                Assert.True(await SavedAsync(viewModel));
                Assert.NotEqual(before, File.ReadAllText(path));
                Assert.Equal("design.jsynth - JuliaSynth", viewModel.Title);
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void A_design_never_saved_is_kept_with_its_changes_for_the_next_start()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel first = new();
                first.StartUp();
                first.OpenExample(Examples.All.Single(e => e.Name == "PageRank"));
                MainViewModel.Nodes[0].Row += 3;
                int row = MainViewModel.Nodes[0].Row;
                Assert.True(await SavedAsync(first));
                Assert.Null(first.FilePath);
                first.EndJulia();

                // Closed without a word: started again, it is there as it was.
                MainViewModel.LoadStartingDesign();
                MainViewModel second = new();
                second.StartUp();
                Assert.False(second.IsStartPageOpen);
                Assert.Null(second.FilePath);
                Assert.Equal(row, MainViewModel.Nodes[0].Row);
                second.EndJulia();
            });
        }

        [Fact]
        public void Turned_off_it_leaves_changes_unsaved_and_stays_off()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                viewModel.StartUp();
                viewModel.AutoSave = false;
                string path = Path.Combine(_folder, "design.jsynth");
                viewModel.SaveDesign(path);

                MainViewModel.Nodes[0].Row += 3;
                Assert.False(await SavedAsync(viewModel));
                Assert.False(viewModel.AutoSaveNow());
                Assert.False(AppSettings.Load().AutoSave);
                viewModel.EndJulia();

                MainViewModel next = new();
                next.StartUp();
                Assert.False(next.AutoSave);
                next.EndJulia();
            });
        }
    }
}
