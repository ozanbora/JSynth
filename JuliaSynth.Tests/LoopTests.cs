using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Going through files one at a time, with inputs taking their next values after each file, as
    /// <c>total = add(total, X)</c> in a loop: the nodes after the files are worked out again for each.
    /// </summary>
    public sealed class LoopTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public LoopTests()
        {
            _ = Directory.CreateDirectory(Frames);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            foreach ((string name, int value) in new[] { ("a.csv", 1), ("b.csv", 2), ("c.csv", 4) })
            {
                File.WriteAllText(Path.Combine(Frames, name), $"{value}\n");
            }
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private string Frames => Path.Combine(_folder, "frames");

        /// <summary>
        /// Files (CSV, one at a time) and total → add → Display; add is total's next value.
        /// </summary>
        private (FilesNode Files, ArrayBuilder Total, Function Add, DisplayNode Shown) Design()
        {
            new MainViewModel().NewDesign();
            FilesNode files = new() { Folder = Frames, EachFile = true };
            ArrayBuilder total = new() { Kind = "zeros", Shape = JuliaTypes.Matrix };
            foreach (Argument size in total.Parameters.OfType<Argument>())
            {
                size.Value = "1";
            }

            Function add = new() { Name = "add", ReturnType = "Matrix{Float64}", Body = "S .+ X" };
            add.Parameters.Add(new Parameter("S", "Matrix{Float64}"));
            add.Parameters.Add(new Parameter("X", "Matrix{Float64}"));
            DisplayNode shown = new();
            MainViewModel.Nodes.AddRange([files, total, add, shown]);
            Assert.Null(MainViewModel.Connect(total, add, add.Parameters[0]));
            Assert.Null(MainViewModel.Connect(files, add, add.Parameters[1]));
            Assert.Null(MainViewModel.Connect(add, total, total.Next!));
            Assert.Null(MainViewModel.Connect(add, shown, shown.Input));
            return (files, total, add, shown);
        }

        [Fact]
        public void A_next_value_is_of_the_inputs_own_type_and_makes_no_loop()
        {
            Ui.Run(() =>
            {
                (FilesNode files, ArrayBuilder total, Function add, _) = Design();
                Float x = new();
                Function half = new() { Name = "half", ReturnType = "Float32", Body = "x / 2" };
                half.Parameters.Add(new Parameter("x", "Float32"));
                MainViewModel.Nodes.AddRange([x, half]);

                // Wired back into the input the value came from: no loop, as it is for the next file.
                Assert.Null(MainViewModel.Connect(x, half, half.Parameters[0]));
                Assert.Null(MainViewModel.Connect(half, x, x.Next!));

                Assert.Equal("Matrix{Float64} can't replace a Float32: the next value takes its place.", MainViewModel.Connect(add, x, x.Next!));
                Assert.Equal("An input already keeps its value from file to file: wire the value it takes next.", MainViewModel.Connect(x, x, x.Next!));

                // Only input nodes take a next value; a file loop's frame is what goes through.
                Assert.Null(files.Next);
                Assert.Null(add.Next);
                Assert.Equal("Matrix{Float64}", files.OutputType);
                files.EachFile = false;
                Assert.Equal(Function.AnyType, files.OutputType);
            });
        }

        [Fact]
        public void The_script_works_out_the_nodes_after_the_files_for_each_file_and_then_the_inputs_take_their_next_values()
        {
            Ui.Run(() =>
            {
                (FilesNode files, ArrayBuilder total, _, _) = Design();
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);

                // Plain Julia: the start before the loop, the reassignment at the end of it.
                string[] parts = plain.Split("# The loop:");
                Assert.Equal(2, parts.Length);
                Assert.Contains("B1 = zeros(", parts[0]);
                Assert.Contains($"for _path in JuliaSynthFiles(raw\"{Frames}\", r\"^.*\\.csv$\"i, path -> readdlm(path, ',', Float64)).paths", parts[1]);
                Assert.Contains("    global frame1 = (path -> readdlm(path, ',', Float64))(_path)", parts[1]);
                Assert.Contains("    global add_out = add(B1, frame1)", parts[1]);
                Assert.EndsWith("    global B1 = add_out\nend\n", parts[1]);

                // A run goes through them too, showing how far it got; the total's values change from file to file.
                Assert.False(run.Streams);
                Assert.Contains($"_jsynth_each(\"{files.ID}\", () -> JuliaSynthFiles(", run.Text);
                Assert.Contains(total.ID, run.Looped);
                Assert.Contains(files.ID, run.Looped);

                // Not one at a time, nothing loops, and the next value is said to be unused.
                files.EachFile = false;
                string once = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.DoesNotContain("# The loop:", once);
                Assert.Contains("# B1 keeps its value: it takes add_out for the next file only when files are gone through one at a time.", once);
            });
        }

        [Fact]
        public void The_next_wire_is_saved_and_copied_with_the_input()
        {
            Ui.Run(() =>
            {
                (_, ArrayBuilder total, Function add, _) = Design();
                MainViewModel viewModel = new();
                string path = Path.Combine(_folder, "loop.jsynth");
                viewModel.SaveDesign(path);
                viewModel.OpenDesign(path);

                ArrayBuilder reopened = MainViewModel.Nodes.OfType<ArrayBuilder>().Single();
                Assert.Equal(total.Next!.ID, reopened.Next!.ID);
                Assert.Contains(MainViewModel.Connections, c => c.TargetID == reopened.ID && c.ParameterID == reopened.Next.ID);

                // A copy of both ends keeps its own next wire.
                foreach (Node node in MainViewModel.Nodes)
                {
                    node.IsSelected = node is ArrayBuilder or Function;
                }

                MainViewModel.DuplicateSelection();
                ArrayBuilder copy = MainViewModel.Nodes.OfType<ArrayBuilder>().Single(b => b.ID != reopened.ID);
                Function copiedAdd = MainViewModel.Nodes.OfType<Function>().Single(f => f.ID != add.ID);
                Assert.NotEqual(reopened.Next.ID, copy.Next!.ID);
                Assert.Contains(MainViewModel.Connections, c => c.SourceID == copiedAdd.ID && c.ParameterID == copy.Next.ID);
            });
        }

        [Fact]
        public void Real_julia_carries_the_total_through_the_files_and_the_app_shows_the_last()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                (FilesNode files, ArrayBuilder total, Function add, DisplayNode shown) = Design();

                // The exported script, as plain Julia: 0 + 1 + 2 + 4.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                using (JuliaSession session = new(julia.Path))
                {
                    ConcurrentQueue<OutputLine> output = new();
                    RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                    string printed = string.Join("\n", output.Select(l => l.Text));
                    Assert.True(result.Succeeded, printed);
                    Assert.Contains("7.0", printed);
                }

                // In the app: every value its last, said once the loop is done.
                MainViewModel viewModel = new() { Julia = julia };
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                string log = string.Join("\n", viewModel.Output.Select(o => o.Text));
                Assert.EndsWith(" 7.0", total.Result?.Text ?? log);
                Assert.Equal("file 3 of 3: c.csv", files.Result?.Text);
                Assert.Single(viewModel.Output, o => o.Text.StartsWith("add_out = "));
                Assert.True(viewModel.Output.Any(o => o.Text == "B1 = 1×1 Matrix{Float64}   (Matrix{Float64})"), log);
                viewModel.EndJulia();
            });
        }
    }
}
