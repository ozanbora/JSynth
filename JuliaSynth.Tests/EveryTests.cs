using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The Every node: a clock, the design a stream ticking every so many seconds.
    /// </summary>
    public sealed class EveryTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public EveryTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static Function Fn(string name, string body, params string[] parameters)
        {
            Function f = new() { Name = name, ReturnType = Function.AnyType, Body = body };
            foreach (string p in parameters)
            {
                f.Parameters.Add(new Parameter(p, Function.AnyType));
            }

            return f;
        }

        /// <summary>
        /// A clock ticking <paramref name="limit"/> times: the tick squared, the ticks added up from tick to tick, and the
        /// seconds since the start.
        /// </summary>
        private static (EveryNode Clock, Function Square, Integer Sum, Function Elapsed) Design(long limit, double seconds = 0.1)
        {
            new MainViewModel().NewDesign();
            EveryNode clock = new() { Seconds = seconds, Limit = limit, Row = 10, Column = 10 };
            Function square = Fn("square", "n^2", "n");
            Integer sum = new() { SelectedType = "64-bit", Value = 0, Row = 25, Column = 10 };
            Function add = Fn("add", "s + n", "s", "n");
            Function elapsed = Fn("elapsed", "t", "t");
            MainViewModel.Nodes.AddRange([clock, square, sum, add, elapsed]);
            Assert.Null(MainViewModel.Connect(clock, square, square.Parameters[0]));
            Assert.Null(MainViewModel.Connect(sum, add, add.Parameters[0]));
            Assert.Null(MainViewModel.Connect(clock, add, add.Parameters[1]));
            Assert.Null(MainViewModel.Connect(add, sum, sum.Next!));
            Assert.Null(MainViewModel.Connect(clock, elapsed, elapsed.Parameters[0], EveryNode.SecondsOutput));
            return (clock, square, sum, elapsed);
        }

        [Fact]
        public void A_clock_makes_the_design_a_stream_ticking_and_says_so_on_the_node()
        {
            Ui.Run(() =>
            {
                (EveryNode clock, _, _, _) = Design(limit: 0, seconds: 2);
                Assert.Equal(("every 2 s", "until stopped"), (clock.Label, clock.LimitText));
                clock.Limit = 100;
                Assert.Equal(("every 2 s", "100 ticks"), (clock.Label, clock.LimitText));
                clock.Seconds = 120;
                Assert.Equal("every 2 min", clock.Label);

                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, stopFile: "stop");
                Assert.True(script.Streams);
                Assert.Same(clock, script.Clock);
                Assert.Contains("_jsynth_every(120, 100; stop = ", script.Text);
                Assert.Contains("_jsynth_tick(", script.Text);

                // Two clocks: one stream, the other said not to be it.
                EveryNode other = new();
                MainViewModel.Nodes.Add(other);
                Function f = Fn("g", "n", "n");
                MainViewModel.Nodes.Add(f);
                Assert.Null(MainViewModel.Connect(other, f, f.Parameters[0]));
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains("A design has one stream", plain);
            });
        }

        [Fact]
        public void Real_julia_ticks_carrying_a_state_from_tick_to_tick_in_the_app_and_as_plain_julia()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                (EveryNode clock, Function square, Integer sum, Function elapsed) = Design(limit: 5);

                // Plain Julia: five ticks, a tenth of a second apart.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text
                    + "\nprintln(\"sum \", z1, \" last \", square_out, \" t \", elapsed_out)";
                using (JuliaSession session = new(julia.Path))
                {
                    ConcurrentQueue<OutputLine> output = new();
                    RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                    string printed = string.Join("\n", output.Select(l => l.Text));
                    Assert.True(result.Succeeded, plain + "\n" + printed);
                    Assert.Matches(@"sum 15 last 25 t 0\.[3-9]", printed);
                }

                // The app: a stream ending by itself after the fifth tick.
                MainViewModel viewModel = new() { Julia = julia };
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                string log = string.Join("\n", viewModel.Output.Select(o => o.Text));
                Assert.Equal("25", square.Result?.Text ?? log);
                Assert.StartsWith("tick 5, 0.", clock.Result?.Text ?? log, StringComparison.Ordinal);
                Assert.Contains("Ticking every 0.1 s, 5 ticks", log);
                Assert.False(viewModel.IsStreaming);
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Real_julia_works_the_nodes_after_a_button_out_again_at_each_press()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // The tick squared, and the presses added up from press to press.
                new MainViewModel().NewDesign();
                ButtonNode button = new() { Row = 10, Column = 10 };
                Function square = Fn("square", "n^2", "n");
                Integer sum = new() { SelectedType = "64-bit", Value = 0, Row = 25, Column = 10 };
                Function add = Fn("add", "s + n", "s", "n");
                MainViewModel.Nodes.AddRange([button, square, sum, add]);
                Assert.Null(MainViewModel.Connect(button, square, square.Parameters[0]));
                Assert.Null(MainViewModel.Connect(sum, add, add.Parameters[0]));
                Assert.Null(MainViewModel.Connect(button, add, add.Parameters[1]));
                Assert.Null(MainViewModel.Connect(add, sum, sum.Next!));

                // Exported, with no button to press: pressed once.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text + "\nprintln(\"sum \", z1, \" last \", square_out)";
                using (JuliaSession session = new(julia.Path))
                {
                    System.Collections.Concurrent.ConcurrentQueue<OutputLine> output = new();
                    RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                    Assert.True(result.Succeeded, plain + "\n" + string.Join("\n", output.Select(l => l.Text)));
                    Assert.Contains(output, l => l.Text == "sum 1 last 1");
                }

                // In the app: the first press runs the design, the next two work the nodes after it out again.
                MainViewModel viewModel = new() { Julia = julia };
                Avalonia.Controls.Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1200, Height = 800 };
                window.Show();
                Avalonia.Controls.Button press = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.Button>().Single(b => b.Name == "PressButton");
                string Log() => string.Join("\n", viewModel.Output.Select(o => o.Text));

                press.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.True(await Wait.Until(() => square.Result?.Text == "1", 60), Log());
                Assert.True(viewModel.IsStreaming);
                press.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.True(await Wait.Until(() => square.Result?.Text == "4", 30), Log());
                press.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.True(await Wait.Until(() => square.Result?.Text == "9" && sum.Result?.Text == "6", 30), Log());
                Assert.StartsWith("press 3, ", button.Result?.Text, StringComparison.Ordinal);
                Assert.Contains("Waiting for presses of the button", Log());

                ((System.Windows.Input.ICommand)viewModel.Stop).Execute(null);
                Assert.True(await Wait.Until(() => !viewModel.IsRunning, 30), Log());
                window.Close();
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Real_julia_runs_the_live_examples()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // Live Monte Carlo π: 100 ticks of 20,000 darts close in on π.
                MainViewModel viewModel = new() { Julia = julia };
                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Live Monte Carlo π"));
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                string Log() => string.Join("\n", viewModel.Output.Select(o => o.Text));
                Function estimate = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "estimate");
                Assert.InRange(double.Parse(estimate.Result?.Text ?? "0", System.Globalization.CultureInfo.InvariantCulture), Math.PI - 0.01, Math.PI + 0.01);
                Assert.StartsWith("tick 100, ", MainViewModel.Nodes.OfType<EveryNode>().Single().Result?.Text ?? Log(), StringComparison.Ordinal);
                Assert.StartsWith("100-element", MainViewModel.Nodes.OfType<WindowNode>().Single().Result?.Text ?? Log(), StringComparison.Ordinal);

                // Random walk: three presses, 3,000 steps.
                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Random walk"));
                ButtonNode button = MainViewModel.Nodes.OfType<ButtonNode>().Single();
                Function position = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "position");
                viewModel.Press(button);
                Assert.True(await Wait.Until(() => position.Result?.Text.StartsWith("after 1 press: 1000 steps", StringComparison.Ordinal) == true, 60), Log());
                viewModel.Press(button);
                viewModel.Press(button);
                Assert.True(await Wait.Until(() => position.Result?.Text.StartsWith("after 3 presses: 3000 steps", StringComparison.Ordinal) == true, 30), Log());
                ((System.Windows.Input.ICommand)viewModel.Stop).Execute(null);
                Assert.True(await Wait.Until(() => !viewModel.IsRunning, 30), Log());
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Real_julia_ticks_until_stopped()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                (EveryNode clock, Function square, _, _) = Design(limit: 0, seconds: 0.05);
                MainViewModel viewModel = new() { Julia = julia };
                System.Threading.Tasks.Task run = viewModel.RunDesignAsync();
                // "tick 3, 0.1 s in": the third tick or later.
                int Ticks() => clock.Result?.Text is string text && text.StartsWith("tick ", StringComparison.Ordinal) ? int.Parse(text[5..text.IndexOf(',')]) : 0;
                Assert.True(await Wait.Until(() => Ticks() >= 3, 60), string.Join("\n", viewModel.Output.Select(o => o.Text)));
                Assert.True(viewModel.IsStreaming);
                Assert.StartsWith("Streaming every 0.05 s: tick ", viewModel.StreamText, StringComparison.Ordinal);

                ((System.Windows.Input.ICommand)viewModel.Stop).Execute(null);
                await run;
                Dispatcher.UIThread.RunJobs();
                Assert.False(viewModel.IsStreaming);
                Assert.Equal(ReportStatus.Ok, square.Result?.Status);
                viewModel.EndJulia();
            });
        }
    }
}
