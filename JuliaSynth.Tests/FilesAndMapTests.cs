using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using JuliaSynth.Controls;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Going through the files of a folder: the Files node, the Map node applying a function to each
    /// value, and the function port it takes the function on.
    /// </summary>
    public sealed class FilesAndMapTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public FilesAndMapTests()
        {
            _ = Directory.CreateDirectory(Path.Combine(_folder, "sub.csv"));
            foreach (string name in new[] { "b.csv", "A.CSV", "c.mtx", "notes.txt", "run-1.csv", "run-22.csv" })
            {
                File.WriteAllText(Path.Combine(_folder, name), "1,2\n3,4\n");
            }
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private static Function OneValue(string name = "total")
        {
            Function function = new() { Name = name, ReturnType = "Float64", Body = "sum(A)" };
            function.Parameters.Add(new Parameter("A", "Matrix{Float64}"));
            return function;
        }

        [Fact]
        public void The_files_matching_the_pattern_are_counted_ignoring_case_and_folders()
        {
            FilesNode files = new() { Folder = _folder };

            Assert.Null(files.Error);
            Assert.Equal("4 files: A.CSV, b.csv, run-1.csv, …", files.Summary);

            files.Pattern = "run-?.csv";
            Assert.Equal("1 file: run-1.csv", files.Summary);

            files.Pattern = "*.parquet";
            Assert.Equal("No file in the folder matches *.parquet.", files.Error);
        }

        [Fact]
        public void The_pattern_follows_the_format_until_it_is_changed_by_hand()
        {
            FilesNode files = new();
            Assert.Equal("*.csv", files.Pattern);

            files.Format = FileFormats.MatrixMarket;
            Assert.Equal("*.mtx", files.Pattern);
            Assert.Equal(JuliaTypes.SparseMatrix, files.Shape);

            files.Pattern = "run-*.mtx";
            files.Format = FileFormats.Jld2;
            Assert.Equal("run-*.mtx", files.Pattern);
        }

        [Theory]
        [InlineData("", "Pick a folder.")]
        [InlineData("relative/runs", null)]
        public void A_folder_is_needed_and_a_relative_one_is_left_to_Julia(string folder, string? error)
        {
            Assert.Equal(error, new FilesNode { Folder = folder }.Error);
        }

        [Fact]
        public void A_missing_folder_or_a_pattern_with_quotes_or_folders_is_an_error()
        {
            Assert.Equal("The folder doesn't exist.", new FilesNode { Folder = Path.Combine(_folder, "missing") }.Error);
            Assert.NotNull(new FilesNode { Folder = _folder, Pattern = "a\".csv" }.Error);
            Assert.NotNull(new FilesNode { Folder = _folder, Pattern = "sub/*.csv" }.Error);
        }

        [Theory]
        [InlineData("*.csv", @"^.*\.csv$")]
        [InlineData("run-?.mtx", @"^run-.\.mtx$")]
        [InlineData("", "^.*$")]
        public void A_name_pattern_becomes_a_regular_expression(string pattern, string regex)
        {
            Assert.Equal(regex, FilesNode.PatternRegex(pattern));
        }

        [Fact]
        public void Each_file_is_read_like_a_single_file_of_its_format()
        {
            FilesNode files = new() { Folder = "data", ElementType = "Float32", HasHeader = true };
            Assert.Equal("JuliaSynthFiles(raw\"data\", r\"^.*\\.csv$\"i, path -> readdlm(path, ',', Float32; skipstart = 1))", files.Julia);
            Assert.Equal("Matrix{Float32}", files.EachType);

            ArrayFile file = new() { Path = "data/a.csv", ElementType = "Float32", HasHeader = true };
            Assert.Equal("readdlm(raw\"data/a.csv\", ',', Float32; skipstart = 1)", file.Julia);

            files.Format = FileFormats.MatrixMarket;
            Assert.EndsWith("path -> SparseMatrixCSC{Float32, Int64}(MatrixMarket.mmread(path)))", files.Julia);
            Assert.Equal("MatrixMarket, SparseArrays", files.Package);
        }

        [Fact]
        public void Only_a_function_of_one_value_can_be_wired_into_a_function_port()
        {
            MapNode map = new();
            Function two = new() { Name = "two" };
            two.Parameters.Add(new Parameter("a", Function.AnyType));
            two.Parameters.Add(new Parameter("b", Function.AnyType));

            Assert.Null(Wiring.Validate(MainViewModel.Nodes, [], OneValue(), map, map.Apply));
            Assert.Equal("Only a function can be wired here: the one to apply to each value.", Wiring.Validate(MainViewModel.Nodes, [], new Float(), map, map.Apply));
            Assert.Contains("must take one", Wiring.Validate(MainViewModel.Nodes, [], two, map, map.Apply));

            // Its value can still go where values go.
            Assert.Null(Wiring.Validate(MainViewModel.Nodes, [], OneValue(), map, map.Items));
            Assert.Null(Wiring.Validate(MainViewModel.Nodes, [], new FilesNode(), map, map.Items));

            // The wire is drawn as fitting (not flagged) until the function takes more values.
            Function applied = OneValue();
            Assert.True(Wiring.Fits(applied, map, map.Apply));
            applied.Parameters.Add(new Parameter("B", Function.AnyType));
            Assert.False(Wiring.Fits(applied, map, map.Apply));
        }

        /// <summary>
        /// Files → Map(total) → Display, in the current design.
        /// </summary>
        private static (FilesNode Files, Function Total, MapNode Map) WireFilesThroughMap(string folder)
        {
            MainViewModel.LoadStartingDesign();
            FilesNode files = new() { Folder = folder };
            Function total = OneValue();
            MapNode map = new();
            DisplayNode shown = new();
            MainViewModel.Nodes.AddRange([files, total, map, shown]);
            Assert.Null(MainViewModel.Connect(files, map, map.Items));
            Assert.Null(MainViewModel.Connect(total, map, map.Apply));
            Assert.Null(MainViewModel.Connect(map, shown, shown.Input));
            return (files, total, map);
        }

        [Fact]
        public void A_run_maps_the_function_over_the_files_and_the_function_has_no_value_of_its_own()
        {
            Ui.Run(() =>
            {
                WireFilesThroughMap(_folder);

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);
                JuliaScript plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false);

                Assert.Contains(FilesNode.Prelude.TrimEnd(), run.Text);
                Assert.Contains(MapNode.Prelude.TrimEnd(), run.Text);
                Assert.Contains("global mapped1 = _jsynth_map(total, files1)", run.Text);
                Assert.Contains("DelimitedFiles", run.Packages);

                // A plain script defines the files too, and maps without the error naming.
                Assert.Contains(FilesNode.Prelude.TrimEnd(), plain.Text);
                Assert.DoesNotContain("_jsynth_map", plain.Text);
                Assert.Contains("mapped1 = map(total, files1)", plain.Text);

                // total(A) is only applied by the Map: not called on its own, and not said to be unwired.
                Assert.DoesNotContain("total_out", run.Text);
                Assert.DoesNotContain("total_out", plain.Text);
            });
        }

        [Fact]
        public void A_map_says_what_it_is_missing()
        {
            Ui.Run(() =>
            {
                (_, Function total, MapNode map) = WireFilesThroughMap(_folder);
                string Plain() => ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;

                // A parameter added after wiring: it can't be applied to one value any more.
                total.Parameters.Add(new Parameter("B", Function.AnyType));
                Assert.Contains("# mapped1: total takes 2 values; Map applies a function of one value.", Plain());

                _ = MainViewModel.Connections.Remove(MainViewModel.Connections.Single(c => c.ParameterID == map.Apply.ID));
                Assert.Contains("# mapped1: Wire in the function to apply to each value.", Plain());

                _ = MainViewModel.Connections.Remove(MainViewModel.Connections.Single(c => c.ParameterID == map.Items.ID));
                Assert.Contains("# mapped1: Wire values in to go through them.", Plain());
            });
        }

        [Fact]
        public void Files_and_maps_are_saved_and_opened_with_their_wires()
        {
            string settingsBefore = AppSettings.SettingsPath;
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    (FilesNode files, _, MapNode map) = WireFilesThroughMap(_folder);
                    files.Format = FileFormats.Tsv;
                    files.Pattern = "run-*.tsv";
                    files.HasHeader = true;
                    MainViewModel viewModel = new();
                    viewModel.SaveDesign(Path.Combine(_folder, "batch.jsynth"));

                    viewModel.NewDesign();
                    viewModel.OpenDesign(Path.Combine(_folder, "batch.jsynth"));

                    FilesNode reopened = MainViewModel.Nodes.OfType<FilesNode>().Single();
                    Assert.Equal((_folder, FileFormats.Tsv, "run-*.tsv", true), (reopened.Folder, reopened.Format, reopened.Pattern, reopened.HasHeader));
                    MapNode mapped = MainViewModel.Nodes.OfType<MapNode>().Single();
                    Assert.True(mapped.Apply.IsFunctionPort);
                    Assert.Equal(3, MainViewModel.Connections.Count(c => c.TargetID == mapped.ID || c.SourceID == mapped.ID));
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
            }
        }

        [Fact]
        public void Both_nodes_can_be_added_and_the_function_port_looks_different()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Assert.IsType<FilesNode>(MainViewModel.AddNode(nameof(FilesNode), 30, 10));
                MapNode map = Assert.IsType<MapNode>(MainViewModel.AddNode(nameof(MapNode), 30, 30));

                Views.MainWindow window = new() { DataContext = new MainViewModel(), Width = 1400, Height = 900 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Views.MapNode view = window.GetLogicalDescendants().OfType<Views.MapNode>().Single(v => v.DataContext == map);
                Connector[] ports = view.GetLogicalDescendants().OfType<Connector>().Where(c => c.IsInput).ToArray();
                Assert.Equal([map.Items, map.Apply], ports.Select(p => p.Parameter));
                Assert.Equal([false, true], ports.Select(p => p.Classes.Contains("function")));
                Assert.Single(window.GetLogicalDescendants().OfType<Views.Inputs.FilesNode>());
                window.Close();
            });
        }
    }
}
