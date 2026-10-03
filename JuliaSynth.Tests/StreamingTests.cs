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
    /// A design watching a folder runs as a stream: the nodes downstream of the Watch folder node again for
    /// every file written to it, a Running node carrying its state from file to file, until stopped.
    /// </summary>
    public sealed class StreamingTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public StreamingTests()
        {
            _ = Directory.CreateDirectory(Path.Combine(_folder, "watched"));
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private string Watched => Path.Combine(_folder, "watched");

        /// <summary>
        /// A one-number CSV file: one frame of a sensor.
        /// </summary>
        private void Frame(string name, int value)
        {
            // Written under another name and renamed, as a careful sensor does: whole or not there at all.
            string path = Path.Combine(Watched, name);
            File.WriteAllText(path + ".tmp", $"{value}\n");
            File.Move(path + ".tmp", path);
        }

        /// <summary>
        /// Watch folder (CSV) → Running(add) → Display, and a constant computed once, before the stream.
        /// </summary>
        private static (WatchNode Watch, RunningNode Total, Float Once) Design(string folder)
        {
            new MainViewModel().NewDesign();
            WatchNode watch = new() { Folder = folder };
            Function add = new() { Name = "add", ReturnType = "Matrix{Float64}", Body = "S .+ X" };
            add.Parameters.Add(new Parameter("S", "Matrix{Float64}"));
            add.Parameters.Add(new Parameter("X", "Matrix{Float64}"));
            RunningNode total = new();
            DisplayNode shown = new();
            Float once = new() { Value = 2.5 };
            MainViewModel.Nodes.AddRange([watch, add, total, shown, once]);
            Assert.Null(MainViewModel.Connect(watch, total, total.Items));
            Assert.Null(MainViewModel.Connect(add, total, total.Update));
            Assert.Null(MainViewModel.Connect(total, shown, shown.Input));
            return (watch, total, once);
        }

        [Fact]
        public void An_empty_folder_waits_rather_than_failing_and_the_frame_is_what_flows()
        {
            WatchNode watch = new() { Folder = Watched, Format = FileFormats.Png };

            Assert.Null(watch.Error);
            Assert.Equal("No *.png file yet: waiting for the first.", watch.Summary);
            Assert.Equal("Matrix{Float64}", watch.OutputType);
            Assert.Equal("The folder doesn't exist.", new WatchNode { Folder = Path.Combine(_folder, "nowhere") }.Error);
        }

        [Fact]
        public void The_nodes_downstream_of_the_watched_folder_run_for_every_file_the_others_once()
        {
            Ui.Run(() =>
            {
                (WatchNode watch, RunningNode total, Float once) = Design(Watched);

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, stopFile: "stop");
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;

                Assert.True(run.Streams);
                string[] parts = run.Text.Split("# The stream:");
                Assert.Equal(2, parts.Length);
                Assert.Contains("global x1 = Float32(2.5)", parts[0]);
                Assert.Contains($"_jsynth_watch(raw\"{Watched}\", r\"^.*\\.csv$\"i, true, false; stop = raw\"stop\", ready = () -> _jsynth_watching(\"{watch.ID}\")) do _path, _n, _show", parts[1]);
                Assert.Contains("_jsynth_frame_start(_show, _jsynth_before_stream)", parts[1]);
                Assert.Contains("global frame1 = (path -> readdlm(path, ',', Float64))(_path)", parts[1]);
                Assert.Contains("_jsynth_frame(", parts[1]);
                Assert.Contains($"_jsynth_tick(\"{total.ID}\", add, ", parts[1]);

                // Plain Julia watches the folder too, until Ctrl+C.
                Assert.Contains(WatchNode.Prelude.TrimEnd(), plain);
                Assert.Contains("(stop with Ctrl+C)", plain);
                Assert.DoesNotContain("_jsynth_frame", plain);

                // Without a Watch folder, nothing streams.
                _ = MainViewModel.Nodes.Remove(watch);
                Assert.False(ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Streams);
            });
        }

        [Fact]
        public void In_the_app_a_replay_stands_in_for_the_sensor_the_values_show_live_and_stop_ends_it()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // Recorded frames, replayed into the watched folder; a copy left by an earlier replay goes first.
                string recorded = Path.Combine(_folder, "recorded");
                _ = Directory.CreateDirectory(recorded);
                foreach ((string name, int value) in new[] { ("a.csv", 1), ("b.csv", 2), ("c.csv", 4) })
                {
                    File.WriteAllText(Path.Combine(recorded, name), $"{value}\n");
                }

                File.Copy(Path.Combine(recorded, "a.csv"), Path.Combine(Watched, "a.csv"));
                (WatchNode watch, RunningNode total, _) = Design(Watched);
                watch.ReplayFolder = recorded;
                watch.ReplayRate = 20;
                watch.ExistingFirst = false;

                MainViewModel viewModel = new() { Julia = julia };
                Task run = viewModel.RunDesignAsync();
                Stopwatch clock = Stopwatch.StartNew();
                while (total.Result?.Text.EndsWith(" 7.0") != true && clock.Elapsed < TimeSpan.FromSeconds(60))
                {
                    await Task.Delay(50);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                string log = string.Join("\n", viewModel.Output.Select(o => o.Text));
                Assert.True(viewModel.IsStreaming, log);
                Assert.True(total.Result?.Text.EndsWith(" 7.0") == true, $"{total.Result?.Text}\n{log}");
                Assert.Contains("3 files so far; the latest: c.csv", viewModel.StreamText);
                Assert.Equal("3 files: a.csv, b.csv, c.csv", watch.Summary);
                Assert.Contains(viewModel.Output, o => o.Text.Contains("Removed the 1 copies an earlier replay left"));

                ((System.Windows.Input.ICommand)viewModel.Stop).Execute(null);
                await run.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.False(viewModel.IsStreaming);
                Assert.False(viewModel.IsRunning);
                Assert.Contains(viewModel.Output, o => o.Text == "Stopped the stream: 3 files so far; the latest: c.csv.");
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Real_julia_carries_the_state_through_files_as_they_come_and_stops_when_asked()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                Frame("a.csv", 1);
                Frame("b.csv", 2);
                (WatchNode watch, RunningNode total, _) = Design(Watched);
                string stop = Path.Combine(_folder, "stop");
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder, stop);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                ConcurrentQueue<OutputLine> output = new();
                Task<RunResult> running = session.RunAsync(script.Text, _folder, output.Enqueue, reports.Enqueue);

                async Task Until(Func<bool> condition)
                {
                    Stopwatch clock = Stopwatch.StartNew();
                    while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(60))
                    {
                        await Task.Delay(50);
                    }

                    Assert.True(condition(), string.Join("\n", output.Select(l => l.Text)));
                }

                bool Total(string value) => reports.Any(r => r.Id == total.ID && r.Text.EndsWith(value));

                // The files already there go first: 1, then 1 + 2.
                await Until(() => Total(" 3.0"));
                Assert.Contains(reports, r => r.Id == watch.ID && r.Text == "2 files so far; the latest: b.csv");
                Assert.False(running.IsCompleted);

                // A file written while it runs: 3 + 4.
                Frame("c.csv", 4);
                await Until(() => Total(" 7.0"));
                Assert.Contains(reports, r => r.Id == watch.ID && r.Text == "3 files so far; the latest: c.csv");

                File.WriteAllText(stop, string.Empty);
                RunResult result = await running.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(result.Succeeded, string.Join("\n", output.Select(l => l.Text)));
            });
        }
    }
}
