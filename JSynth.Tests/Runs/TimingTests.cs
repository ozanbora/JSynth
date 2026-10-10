using Avalonia.Threading;
using JSynth.Model;
using JSynth.Runtime;
using JSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Runs
{
    /// <summary>
    /// A run says when each value starts being worked out, lighting its node up, and how long it took.
    /// </summary>
    public sealed class TimingTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public TimingTests()
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
        public void Reports_say_a_value_is_running_and_how_long_it_took()
        {
            Assert.Equal(new Report("a", ReportStatus.Running, "", ""), Reports.Parse($"{Reports.Marker}a\trunning\t\t"));
            Assert.Equal(0.25, Reports.Parse($"{Reports.Marker}a\tok\tInt64\t3\t0.25")?.Seconds);
            Assert.Null(Reports.Parse($"{Reports.Marker}a\tok\tInt64\t3")?.Seconds);

            Assert.Equal(["0.4 ms", "12 ms", "1.3 s", "2 min 5 s"], new[] { 0.0004, 0.012, 1.3, 125.0 }.Select(Node.Duration));
        }

        [Fact]
        public void Real_julia_lights_each_node_up_while_it_runs_and_times_it()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // A slow step and a quick one after it.
                MainViewModel viewModel = new() { Julia = julia };
                viewModel.NewDesign();
                Float x = new() { SelectedType = "64-bit", Value = 2, Row = 10, Column = 10 };
                Function slow = new() { Name = "slow", ReturnType = "Float64", Body = "sleep(0.4); x", Parameters = { new Parameter("x", "Float64") } };
                Function quick = new() { Name = "quick", ReturnType = "Float64", Body = "x + 1", Parameters = { new Parameter("x", "Float64") } };
                MainViewModel.Nodes.AddRange([x, slow, quick]);
                Assert.Null(MainViewModel.Connect(x, slow, slow.Parameters[0]));
                Assert.Null(MainViewModel.Connect(slow, quick, quick.Parameters[0]));

                // Each lit up before its result came, and only then.
                List<string> seen = [];
                foreach (Node node in MainViewModel.Nodes)
                {
                    node.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName is nameof(Node.IsRunning) or nameof(Node.Result) && (e.PropertyName != nameof(Node.Result) || node.Result is not null))
                        {
                            seen.Add($"{((Function?)(node as Function))?.Name ?? "x"} {e.PropertyName}{(e.PropertyName == nameof(Node.IsRunning) ? $" {node.IsRunning}" : "")}");
                        }
                    };
                }

                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                string log = string.Join("\n", viewModel.Output.Select(o => o.Text));

                int lit = seen.IndexOf("slow IsRunning True");
                Assert.True(lit >= 0, string.Join("\n", seen) + "\n" + log);
                Assert.Equal("slow IsRunning False", seen[lit + 1]);
                Assert.Equal("slow Result", seen[lit + 2]);
                Assert.DoesNotContain(MainViewModel.Nodes, n => n.IsRunning);

                // Timed: the sleep, and next to nothing for the rest; the slowest named after the run.
                Assert.InRange(slow.Result?.Seconds ?? 0, 0.4, 2);
                Assert.InRange(quick.Result?.Seconds ?? 1, 0, 0.1);
                Assert.Matches(@"^(\d{3} ms|\d\.\d s)$", slow.ResultTime);
                Assert.Contains("Took ", slow.ResultTip);
                Assert.Contains(viewModel.Output, o => o.Text.StartsWith("Slowest: slow", StringComparison.Ordinal));
                viewModel.EndJulia();
            });
        }
    }
}
