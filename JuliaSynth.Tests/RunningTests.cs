using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using JuliaSynth.Controls;
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
    /// The Running node: going through values while carrying a state along, as a running average of
    /// frames makes a clutter map.
    /// </summary>
    public sealed class RunningTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public RunningTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// update(C, X): the clutter map C nudged towards the frame X.
        /// </summary>
        private static Function Update(string name = "update")
        {
            Function function = new() { Name = name, ReturnType = "Matrix{Float64}", Body = "0.5 .* C .+ 0.5 .* X" };
            function.Parameters.Add(new Parameter("C", "Matrix{Float64}"));
            function.Parameters.Add(new Parameter("X", "Matrix{Float64}"));
            return function;
        }

        /// <summary>
        /// Files (PNG frames) → Running(update) → Display, in the current design; start not wired.
        /// </summary>
        private (FilesNode Files, Function Update, RunningNode Running, DisplayNode Shown) WireFramesThroughRunning()
        {
            new MainViewModel().NewDesign();
            FilesNode files = new() { Folder = _folder, Format = FileFormats.Png };
            Function update = Update();
            RunningNode running = new();
            DisplayNode shown = new();
            MainViewModel.Nodes.AddRange([files, update, running, shown]);
            Assert.Null(MainViewModel.Connect(files, running, running.Items));
            Assert.Null(MainViewModel.Connect(update, running, running.Update));
            Assert.Null(MainViewModel.Connect(running, shown, shown.Input));
            return (files, update, running, shown);
        }

        [Fact]
        public void Only_a_function_of_the_state_and_a_value_updates_the_state()
        {
            RunningNode running = new();
            Function one = new() { Name = "one" };
            one.Parameters.Add(new Parameter("x", Function.AnyType));

            Assert.Null(Wiring.Validate([], Update(), running, running.Update));
            Assert.Equal("one takes 1 values; it must take two: the state, then a value, to update the state with each value.", Wiring.Validate([], one, running, running.Update));
            Assert.Equal("Only a function can be wired here: the one updating the state with each value.", Wiring.Validate([], new Float(), running, running.Update));

            // Map still takes a function of one value, and Running's values and start take values.
            MapNode map = new();
            Assert.True(Wiring.Fits(one, map, map.Apply));
            Assert.False(Wiring.Fits(Update(), map, map.Apply));
            Assert.Null(Wiring.Validate([], new FilesNode(), running, running.Items));
            Assert.Null(Wiring.Validate([], new Float(), running, running.Start));
        }

        [Fact]
        public void A_run_goes_through_the_values_and_a_plain_script_folds_or_accumulates()
        {
            Ui.Run(() =>
            {
                (_, _, RunningNode running, _) = WireFramesThroughRunning();
                string Run() => ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;
                string Plain() => ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;

                Assert.Contains(RunningNode.Prelude.TrimEnd(), Run());
                Assert.Contains("global running1 = _jsynth_running(update, files1, false)", Run());
                Assert.Contains("running1 = foldl(update, files1)", Plain());
                Assert.DoesNotContain("_jsynth_running", Plain());

                // The function is only called by Running: it has no value of its own.
                Assert.DoesNotContain("update_out", Run());

                Float start = new() { Value = 0 };
                MainViewModel.Nodes.Add(start);
                Assert.Null(MainViewModel.Connect(start, running, running.Start));
                running.KeepEach = true;
                Assert.Equal(JuliaTypes.Vector, running.OutputType);
                Assert.Equal("all", running.KeepLabel);
                Assert.Contains("global running1 = _jsynth_running(update, files1, true; init = x1)", Run());
                Assert.Contains("running1 = collect(Iterators.accumulate(update, files1; init = x1))", Plain());
            });
        }

        [Fact]
        public void Running_says_what_it_is_missing()
        {
            Ui.Run(() =>
            {
                (_, Function update, RunningNode running, _) = WireFramesThroughRunning();
                string Plain() => ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;

                update.Parameters.RemoveAt(1);
                Assert.Contains("# running1: update takes 1 values; Running updates the state with a function of two: the state, then a value.", Plain());

                _ = MainViewModel.Connections.Remove(MainViewModel.Connections.Single(c => c.ParameterID == running.Update.ID));
                Assert.Contains("# running1: Wire in the function updating the state with each value.", Plain());

                _ = MainViewModel.Connections.Remove(MainViewModel.Connections.Single(c => c.ParameterID == running.Items.ID));
                Assert.Contains("# running1: Wire values in to go through them.", Plain());
            });
        }

        [Fact]
        public void Running_is_saved_and_opened_with_its_wires_and_what_it_keeps()
        {
            string settingsBefore = AppSettings.SettingsPath;
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    (_, _, RunningNode running, _) = WireFramesThroughRunning();
                    running.KeepEach = true;
                    MainViewModel viewModel = new();
                    viewModel.SaveDesign(Path.Combine(_folder, "clutter.jsynth"));

                    viewModel.NewDesign();
                    viewModel.OpenDesign(Path.Combine(_folder, "clutter.jsynth"));

                    RunningNode reopened = MainViewModel.Nodes.OfType<RunningNode>().Single();
                    Assert.True(reopened.KeepEach);
                    Assert.True(reopened.Update.IsFunctionPort);
                    Assert.Equal(3, MainViewModel.Connections.Count(c => c.TargetID == reopened.ID || c.SourceID == reopened.ID));

                    // The function still fits after opening: the port is found as Running's update port.
                    Function update = MainViewModel.Nodes.OfType<Function>().Single();
                    Assert.True(Wiring.Fits(update, reopened, reopened.Update));
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
            }
        }

        [Fact]
        public void In_the_window_it_shows_its_three_ports_and_switches_between_last_and_all()
        {
            Ui.Run(() =>
            {
                (_, _, RunningNode running, _) = WireFramesThroughRunning();
                Views.MainWindow window = new() { DataContext = new MainViewModel(), Width = 1400, Height = 900 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Views.RunningNode view = window.GetLogicalDescendants().OfType<Views.RunningNode>().Single(v => v.DataContext == running);
                Connector[] ports = view.GetLogicalDescendants().OfType<Connector>().Where(c => c.IsInput).ToArray();
                Assert.Equal([running.Items, running.Start, running.Update], ports.Select(p => p.Parameter));
                Assert.Equal([false, false, true], ports.Select(p => p.Classes.Contains("function")));

                Button keep = view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "last"));
                keep.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(running.KeepEach);
                Assert.Equal("all", keep.Content);
                window.Close();
            });
        }

        [Fact]
        public void Real_julia_makes_a_clutter_map_of_a_folder_of_frames()
        {
            Ui.Run(async () =>
            {
                // Needs Julia with PNGFiles and ColorTypes, which nothing here installs.
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia
                    || (await new JuliaPackages(julia.Path).FindMissingAsync(["PNGFiles", "ColorTypes"], _folder)).Count > 0)
                {
                    return;
                }

                // Three frames whose pixels are 0, 255 and 255 (8-bit): intensities 0, 1, 1.
                PngTests.WriteGreyPng(Path.Combine(_folder, "frame-1.png"), new[,] { { 0, 0 } }, 8);
                PngTests.WriteGreyPng(Path.Combine(_folder, "frame-2.png"), new[,] { { 255, 255 } }, 8);
                PngTests.WriteGreyPng(Path.Combine(_folder, "frame-3.png"), new[,] { { 255, 255 } }, 8);
                (_, _, RunningNode running, _) = WireFramesThroughRunning();

                using JuliaSession session = new(julia.Path);
                async Task<string> Value()
                {
                    JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);
                    ConcurrentQueue<Report> reports = new();
                    RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);
                    Assert.True(result.Succeeded);
                    Report report = reports.Single(r => r.Id == running.ID);
                    Assert.Equal(ReportStatus.Ok, report.Status);
                    return report.Text;
                }

                // From the first frame: 0 → (0 + 1) / 2 = 0.5 → (0.5 + 1) / 2 = 0.75.
                Assert.Equal("1×2 Matrix{Float64}:\n 0.75  0.75", await Value());

                running.KeepEach = true;
                Assert.StartsWith("3-element Vector{Matrix{Float64}}:\n [0.0 0.0]\n [0.5 0.5]\n [0.75 0.75]", await Value());

                // The plain script gives the same, with foldl.
                running.KeepEach = false;
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                ConcurrentQueue<OutputLine> output = new();
                RunResult exported = await session.RunAsync(plain + "\nprintln(running1)", _folder, output.Enqueue);
                Assert.True(exported.Succeeded, string.Join("\n", output.Select(l => l.Text)));
                Assert.Equal("[0.75 0.75]", output.Last().Text);
            });
        }
    }
}
