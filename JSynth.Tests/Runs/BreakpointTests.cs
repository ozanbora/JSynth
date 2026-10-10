using Avalonia.Threading;
using JSynth.Model;
using JSynth.Runtime;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace JSynth.Tests.Runs
{
    /// <summary>
    /// Breakpoints on wires: a run pauses when a value comes on one, every time or on a condition, until told to go on.
    /// </summary>
    public sealed class BreakpointTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public BreakpointTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// The starting design, f(x, z) then g(f, n), and the wire from f into g.
        /// </summary>
        private static (Function F, Function G, Connection Wire) Starting()
        {
            MainViewModel.LoadStartingDesign();
            Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
            Function g = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g");
            return (f, g, MainViewModel.ConnectionTo(g.Parameters[0])!);
        }

        private static string Run(bool instrumented, string? stopFile = "stop") =>
            ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented, stopFile: stopFile).Text;

        [Fact]
        public void A_run_pauses_where_a_breakpoint_is_on_and_an_exported_script_never()
        {
            Ui.Run(() =>
            {
                (_, _, Connection wire) = Starting();
                string before = Run(instrumented: true);
                Assert.DoesNotContain("_jsynth_break", before);

                MainViewModel.SetBreakpoint(wire, new Breakpoint(true, "x > 3"));
                string paused = Run(instrumented: true);
                Assert.Contains("function _jsynth_break(", paused);
                Assert.Contains($"_jsynth_break(\"{wire.Key}\", f_out, x -> (x > 3), raw\"stop.resume\", raw\"stop\", \"{wire.SourceID}\")", paused);
                Assert.DoesNotContain("_jsynth_break", Run(instrumented: false));
                Assert.DoesNotContain("_jsynth_break(", Run(instrumented: true, stopFile: null));

                // Off: as if there were none.
                MainViewModel.SetBreakpoint(wire, new Breakpoint(false, "x > 3"));
                Assert.Equal(before, Run(instrumented: true));
            });
        }

        [Fact]
        public void A_breakpoint_is_saved_with_the_design_undone_and_edited_from_its_card()
        {
            Ui.Run(() =>
            {
                (_, Function g, Connection wire) = Starting();
                MainViewModel viewModel = new();
                MainViewModel.History.Commit();
                MainViewModel.SetBreakpoint(wire, Breakpoint.Every);
                MainViewModel.History.Commit();

                // The card: on a condition, then off.
                BreakpointEditing card = new(wire, () => { });
                Assert.Equal("Pause on f → g(y)", card.Title);
                card.OnCondition = true;
                card.Condition = "x > 3";
                card.On = false;
                Assert.Equal(new Breakpoint(false, "x > 3"), wire.Breakpoint);
                card.On = true;

                viewModel.SaveDesign(Path.Combine(_folder, "paused.jsynth"));
                viewModel.NewDesign();
                viewModel.OpenDesign(Path.Combine(_folder, "paused.jsynth"));
                Assert.Equal(new Breakpoint(true, "x > 3"), MainViewModel.ConnectionTo(MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "g").Parameters[0])!.Breakpoint);

                // Removed, then undone.
                Connection opened = MainViewModel.Connections.Single(c => c.Breakpoint is not null);
                MainViewModel.History.Commit();
                new BreakpointEditing(opened, () => { }).Remove();
                Assert.Null(opened.Breakpoint);
                MainViewModel.History.Commit();
                ((ICommand)viewModel.Undo).Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(MainViewModel.Connections, c => c.Breakpoint is not null);
            });
        }

        [Fact]
        public void Real_julia_pauses_with_the_values_so_far_and_goes_on_when_told()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                (Function f, Function g, Connection wire) = Starting();
                MainViewModel.SetBreakpoint(wire, Breakpoint.Every);
                MainViewModel viewModel = new() { Julia = julia };
                try
                {
                    string Log() => string.Join("\n", viewModel.Output.Select(o => o.Text));

                    System.Threading.Tasks.Task run = viewModel.RunDesignAsync();
                    Assert.True(await Wait.Until(() => viewModel.IsPaused, 60), Log());
                    Assert.StartsWith("Paused at f → g(y): x = 3.375", viewModel.PausedText, StringComparison.Ordinal);
                    Assert.Same(wire, MainViewModel.PausedWire);
                    Assert.Equal(ReportStatus.Ok, f.Result?.Status);
                    Assert.Null(g.Result);

                    // Going on: g is worked out, and the run ends.
                    ((ICommand)viewModel.RunOrContinue).Execute(null);
                    await run;
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(viewModel.IsPaused);
                    Assert.Null(MainViewModel.PausedWire);
                    Assert.Equal(ReportStatus.Ok, g.Result?.Status);
                }
                finally
                {
                    // Paused or not, Julia ends with the test.
                    viewModel.EndJulia();
                }
            });
        }

        [Fact]
        public void Real_julia_pauses_on_a_condition_and_at_the_next_value_when_told()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // A clock's ticks, squared; paused on the wire from the clock when the tick is a multiple of 5.
                new MainViewModel().NewDesign();
                EveryNode clock = new() { Seconds = 0.02, Limit = 0, Row = 10, Column = 10 };
                Function square = new() { Name = "square", ReturnType = Function.AnyType, Body = "n^2", Row = 10, Column = 30 };
                square.Parameters.Add(new Parameter("n", Function.AnyType));
                MainViewModel.Nodes.AddRange([clock, square]);
                Assert.Null(MainViewModel.Connect(clock, square, square.Parameters[0]));
                MainViewModel.SetBreakpoint(MainViewModel.ConnectionTo(square.Parameters[0])!, new Breakpoint(true, "x % 5 == 0"));

                MainViewModel viewModel = new() { Julia = julia };

                try

                {
                    string Log() => string.Join("\n", viewModel.Output.Select(o => o.Text));
                    System.Threading.Tasks.Task run = viewModel.RunDesignAsync();
                    Assert.True(await Wait.Until(() => viewModel.IsPaused, 60), Log());
                    Assert.Contains("x = 5 (x % 5 == 0)", viewModel.PausedText);
                    Assert.Contains("tick 5", viewModel.PausedText);

                    // To the next: tick 6, whatever the condition; then on to 10.
                    ((ICommand)viewModel.ToNext).Execute(null);
                    Assert.True(await Wait.Until(() => viewModel.IsPaused && viewModel.PausedText.Contains("x = 6", StringComparison.Ordinal), 30), Log());
                    ((ICommand)viewModel.Continue).Execute(null);
                    Assert.True(await Wait.Until(() => viewModel.IsPaused && viewModel.PausedText.Contains("x = 10", StringComparison.Ordinal), 30), Log());

                    // Stopped while paused: the stream ends.
                    ((ICommand)viewModel.Stop).Execute(null);
                    await run;
                    Assert.False(viewModel.IsPaused);
                    Assert.False(viewModel.IsStreaming);

                }

                finally

                {

                    // Paused or not, Julia ends with the test.

                    viewModel.EndJulia();

                }
            });
        }
    }
}
