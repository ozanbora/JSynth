using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Saving and opening design files. Everything happens in a temporary folder, settings included,
    /// so the real settings and designs are never touched.
    /// </summary>
    public sealed class FileTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public FileTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private string PathOf(string name) => Path.Combine(_folder, name);

        private static void Run(Action<MainViewModel> test)
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                test(new MainViewModel());
            });
        }

        [Fact]
        public void A_saved_design_opens_the_same()
        {
            Run(viewModel =>
            {
                Function f = MainViewModel.Nodes.OfType<Function>().First();
                f.Row = 30;
                string parameterID = f.Parameters[0].ID;
                viewModel.SaveDesign(PathOf("a.jsynth"));

                viewModel.NewDesign();
                Assert.Empty(MainViewModel.Nodes);

                viewModel.OpenDesign(PathOf("a.jsynth"));
                Assert.Equal(5, MainViewModel.Nodes.Count);
                Assert.Equal(4, MainViewModel.Connections.Count);
                Function reopened = MainViewModel.Nodes.OfType<Function>().First();
                Assert.Equal(30, reopened.Row);
                Assert.Equal(parameterID, reopened.Parameters[0].ID);
            });
        }

        [Fact]
        public void Saving_names_the_design_and_clears_the_unsaved_mark()
        {
            Run(viewModel =>
            {
                Assert.Equal("Untitled - JuliaSynth", viewModel.Title);

                MainViewModel.Nodes[0].Row += 1;
                Assert.True(viewModel.IsDirty);
                Assert.Equal("Untitled • - JuliaSynth", viewModel.Title);

                viewModel.SaveDesign(PathOf("b.jsynth"));
                Assert.False(viewModel.IsDirty);
                Assert.Equal("b.jsynth - JuliaSynth", viewModel.Title);
            });
        }

        [Fact]
        public void An_old_nodes_json_with_just_the_node_list_still_opens()
        {
            // nodes.json as it was saved before connections existed.
            const string legacy = """
                {
                  "$type": "Avalonia.Collections.AvaloniaList`1[[JuliaSynth.Model.Node, JuliaSynth]], Avalonia.Base",
                  "$values": [
                    { "$type": "JuliaSynth.Model.Float, JuliaSynth", "Column": 11, "Row": 4, "SelectedType": "32-bit", "Value": 0.0 }
                  ]
                }
                """;
            File.WriteAllText(PathOf("nodes.json"), legacy);

            Run(viewModel =>
            {
                viewModel.OpenDesign(PathOf("nodes.json"));

                Node node = Assert.Single(MainViewModel.Nodes);
                Assert.IsType<Float>(node);
                Assert.Equal(4, node.Row);
                Assert.Empty(MainViewModel.Connections);
            });
        }

        [Theory]
        [InlineData("this is not json")]
        // A file naming a type that isn't part of a design must not be able to create it.
        [InlineData("""{"$type":"System.IO.DirectoryInfo, System.Runtime","OriginalPath":"C:\\"}""")]
        public void An_unusable_file_is_refused_and_the_open_design_kept(string content)
        {
            File.WriteAllText(PathOf("bad.jsynth"), content);

            Run(viewModel =>
            {
                Exception error = Assert.ThrowsAny<JsonException>(() => viewModel.OpenDesign(PathOf("bad.jsynth")));

                Assert.True(MainViewModel.IsFileError(error));
                Assert.Equal(5, MainViewModel.Nodes.Count);
                Assert.Null(viewModel.FilePath);
            });
        }

        [Fact]
        public void A_missing_file_is_a_file_error()
        {
            Run(viewModel =>
            {
                Exception error = Assert.ThrowsAny<IOException>(() => viewModel.OpenDesign(PathOf("missing.jsynth")));

                Assert.True(MainViewModel.IsFileError(error));
            });
        }

        [Fact]
        public void Start_up_reopens_the_last_design()
        {
            Run(viewModel =>
            {
                MainViewModel.Nodes[0].Row = 40;
                viewModel.SaveDesign(PathOf("last.jsynth"));
                viewModel.NewDesign();

                MainViewModel restarted = new();
                restarted.StartUp();

                Assert.Equal(Path.GetFullPath(PathOf("last.jsynth")), restarted.FilePath);
                Assert.Equal(40, MainViewModel.Nodes[0].Row);
            });
        }
    }
}
