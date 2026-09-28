using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The nodes at the end of a design: Display and Save to file.
    /// </summary>
    public sealed class OutputNodeTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public OutputNodeTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void Nothing_can_be_wired_from_an_output_node()
        {
            DisplayNode display = new();
            Parameter x = new("x", Function.AnyType);
            Function f = new() { Parameters = { x } };

            Assert.False(display.HasOutput);
            Assert.Equal("This node has no output to wire from.", Wiring.Validate([], display, f, x));
            Assert.Null(Wiring.Validate([], f, display, display.Input));
        }

        [Fact]
        public void A_display_shows_the_value_in_a_run_and_displays_it_in_a_script()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function g = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "g");
                DisplayNode display = new();
                MainViewModel.Nodes.Add(display);
                Assert.Null(MainViewModel.Connect(g, display, display.Input));

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);

                Assert.Equal("shown1", run.Variables[display.ID]);
                Assert.Contains("global shown1 = g_out", run.Text);
                Assert.EndsWith("display(g_out)\n", MainViewModel.ExportScript());
            });
        }

        [Fact]
        public void An_unwired_output_node_is_skipped_with_the_reason()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel.Nodes.AddRange([new DisplayNode(), new SaveFile { Path = "a.csv" }, new SaveFile()]);

                string script = MainViewModel.ExportScript();

                Assert.Contains("# shown1: Wire a value in to show it.", script);
                Assert.Contains("# saved1: Wire a value in to save it.", script);
                Assert.Contains("# saved2: Pick a file to save to.", script);
            });
        }

        [Theory]
        [InlineData("out/result.csv", "writedlm(raw\"out/result.csv\", x, ',')", "DelimitedFiles")]
        [InlineData("result.tsv", "writedlm(raw\"result.tsv\", x, '\\t')", "DelimitedFiles")]
        [InlineData("A.mtx", "MatrixMarket.mmwrite(raw\"A.mtx\", x)", "MatrixMarket, SparseArrays")]
        [InlineData("data.jld2", "JLD2.jldsave(raw\"data.jld2\"; A = x)", "JLD2")]
        public void Saving_writes_in_the_format_of_the_file(string path, string statement, string packages)
        {
            SaveFile save = new() { Path = path };

            Assert.Null(save.Problem);
            Assert.Equal(statement, save.Statement("x"));
            Assert.Equal($"begin\n    {statement}\n    abspath(raw\"{path}\")\nend", save.Julia("x"));
            Assert.Equal(packages, string.Join(", ", save.Packages));
        }

        [Fact]
        public void Only_sparse_matrices_are_saved_as_matrix_market()
        {
            SaveFile save = new() { Path = "A.mtx" };
            MatrixNode dense = new();
            ArrayBuilder sparse = new() { Kind = "sprand", Shape = "Matrix" };

            Assert.Equal("SparseMatrixCSC", save.Input.Type);
            Assert.NotNull(Wiring.Validate([], dense, save, save.Input));
            Assert.Null(Wiring.Validate([], sparse, save, save.Input));

            save.Format = ArrayFile.Csv;
            Assert.Null(Wiring.Validate([], dense, save, save.Input));
        }

        [Fact]
        public void A_jld2_value_needs_a_name()
        {
            SaveFile save = new() { Path = "data.jld2", Variable = "2x" };

            Assert.Equal("Give the value a name to store it under.", save.Problem);
            Assert.Equal(save.Problem, save.Error);
        }

        [Fact]
        public void Output_nodes_keep_their_wires_when_duplicated_and_undone()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function g = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "g");
                SaveFile save = new() { Path = "out.tsv", Format = ArrayFile.Csv };
                DisplayNode display = new();
                MainViewModel.Nodes.AddRange([save, display]);
                Assert.Null(MainViewModel.Connect(g, save, save.Input));
                Assert.Null(MainViewModel.Connect(g, display, display.Input));
                MainViewModel.History.Commit();

                MainViewModel.SelectOnly(g);
                save.IsSelected = true;
                MainViewModel.DuplicateSelection();
                SaveFile copy = MainViewModel.Nodes.OfType<SaveFile>().Single(s => s.IsSelected);
                Assert.Equal(ArrayFile.Csv, copy.Format);
                Assert.NotEqual(save.Input.ID, copy.Input.ID);
                Assert.Single(MainViewModel.Connections, c => c.ParameterID == copy.Input.ID);

                MainViewModel.History.Commit();
                MainViewModel.History.Undo();
                Assert.Single(MainViewModel.Nodes.OfType<SaveFile>());
                Assert.Equal(6, MainViewModel.Connections.Count);
            });
        }

        [Fact]
        public void Moving_a_node_keeps_the_results_but_editing_one_clears_them()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                DisplayNode display = new();
                MainViewModel.Nodes.Add(display);
                display.Result = new Report(display.ID, ReportStatus.Ok, "Float64", "1.6875");
                Assert.Equal("1.6875", display.Shown);
                Assert.Equal("Float64", display.ShownType);

                display.Row += 3;
                MainViewModel.Nodes.OfType<Float>().Single().Color = Avalonia.Media.Colors.Red;
                Assert.Equal("1.6875", display.Shown);

                MainViewModel.Nodes.OfType<Float>().Single().Value = 2;
                Assert.False(display.HasResult);
                Assert.StartsWith("Run the design", display.Shown);
            });
        }

        [Fact]
        public async Task Real_julia_shows_values_and_writes_files()
        {
            // Nothing to check against without Julia.
            if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
            {
                return;
            }

            string script = string.Empty;
            string displayID = string.Empty;
            string saveID = string.Empty;
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function g = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "g");
                ArrayBuilder range = new() { Kind = "range" };
                DisplayNode display = new();
                SaveFile save = new() { Path = "out/range.csv" };
                MainViewModel.Nodes.AddRange([range, display, save]);
                Assert.Null(MainViewModel.Connect(g, display, display.Input));
                Assert.Null(MainViewModel.Connect(range, save, save.Input));
                (displayID, saveID) = (display.ID, save.ID);
                script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;
            });

            // Julia doesn't make missing folders; the user picks an existing one.
            _ = Directory.CreateDirectory(Path.Combine(_folder, "out"));
            JuliaRunner runner = new(julia.Path);
            ConcurrentQueue<Report> reports = new();
            runner.Reported += reports.Enqueue;
            RunResult result = await runner.RunAsync(script, _folder);

            Assert.True(result.Succeeded);
            Assert.Equal("1.6875", reports.Single(r => r.Id == displayID).Text);
            Report saved = reports.Single(r => r.Id == saveID);
            Assert.Equal(ReportStatus.Ok, saved.Status);
            Assert.Equal(Path.GetFullPath(Path.Combine(_folder, "out", "range.csv")), Path.GetFullPath(saved.Text));
            string[] lines = File.ReadAllLines(saved.Text);
            Assert.Equal(100, lines.Length);
            Assert.Equal("0.0", lines[0]);
            Assert.Equal("1.0", lines[^1]);
        }
    }
}
