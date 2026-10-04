using Avalonia.Threading;
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
    /// What going through files one at a time needs besides inputs taking next values: the last few values
    /// (Window), passing a value on a condition only (Only when), the file's name, and saving a row per file.
    /// </summary>
    public sealed class LoopFeatureTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public LoopFeatureTests()
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

        private string Rows => Path.Combine(_folder, "levels.csv");

        private static Function Define(string name, string returnType, string body, params (string Name, string Type)[] parameters)
        {
            Function function = new() { Name = name, ReturnType = returnType, Body = body };
            foreach ((string parameterName, string type) in parameters)
            {
                function.Parameters.Add(new Parameter(parameterName, type));
            }

            return function;
        }

        private sealed record Loop(FilesNode Files, WindowNode Window, WhenNode First, DisplayNode Last2, DisplayNode FirstHigh, SaveFile Levels);

        /// <summary>
        /// Files (1, 2, 4, one at a time) → level → the last 2 levels, a row per file, and the name of the first
        /// file whose level is over 1.5 (alarms counts the high levels so far, so only the first passes).
        /// </summary>
        private Loop Design()
        {
            new MainViewModel().NewDesign();
            FilesNode files = new() { Folder = Frames, EachFile = true };
            Function level = Define("level", "Float64", "X[1]", ("X", "Matrix{Float64}"));
            WindowNode window = new() { Size = 2 };
            DisplayNode last2 = new();
            FileNameNode name = new();
            Function high = Define("high", "Bool", "x > 1.5", ("x", "Float64"));
            Integer alarms = new() { SelectedType = "64-bit", Value = 0 };
            Function count = Define("count", "Int64", "c + h", ("c", "Int64"), ("h", "Bool"));
            Function firstTime = Define("first_time", "Bool", "h && c == 0", ("h", "Bool"), ("c", "Int64"));
            WhenNode first = new();
            DisplayNode firstHigh = new();
            SaveFile levels = new() { Path = Rows, AppendRows = true };
            MainViewModel.Nodes.AddRange([files, level, window, last2, name, high, alarms, count, firstTime, first, firstHigh, levels]);

            foreach ((Node source, Node target, Parameter parameter) in new (Node, Node, Parameter)[]
            {
                (files, level, level.Parameters[0]), (level, window, window.Value), (window, last2, last2.Input),
                (level, high, high.Parameters[0]), (alarms, count, count.Parameters[0]), (high, count, count.Parameters[1]), (count, alarms, alarms.Next!),
                (high, firstTime, firstTime.Parameters[0]), (alarms, firstTime, firstTime.Parameters[1]),
                (name, first, first.Value), (firstTime, first, first.Condition), (first, firstHigh, firstHigh.Input),
                (level, levels, levels.Input),
            })
            {
                Assert.Null(MainViewModel.Connect(source, target, parameter));
            }

            return new(files, window, first, last2, firstHigh, levels);
        }

        [Fact]
        public void The_script_keeps_a_window_holds_back_after_a_false_condition_and_adds_a_row_per_file()
        {
            Ui.Run(() =>
            {
                Loop loop = Design();
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                string[] parts = plain.Split("# The loop:");

                // Before the loop: the window's values, and the file of rows started anew.
                Assert.Contains("window1_values = []", parts[0]);
                Assert.Contains($"rm(raw\"{Rows}\"; force = true)", parts[0]);
                Assert.Contains(WindowNode.Prelude.TrimEnd(), parts[0]);

                // In it: the last 2 levels, the file's name, a row after the name, and the first high level's name.
                Assert.Contains("global window1 = _jsynth_window!(window1_values, level_out, 2)", parts[1]);
                Assert.Contains("global name1 = basename(_path)", parts[1]);
                Assert.Contains($"open(io -> writedlm(io, permutedims([basename(_path); vec(collect(level_out))]), ','), raw\"{Rows}\", \"a\")", parts[1]);
                Assert.Contains("global passed1_ok = first_time_out\n    if passed1_ok\n        global passed1 = name1\n    end", parts[1]);
                Assert.Contains("    if passed1_ok\n        display(passed1)\n    end", parts[1]);

                // Outside a loop there is no file name; a row has no name before it.
                loop.Files.EachFile = false;
                string once = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains("# name1: There's a file name only while files are gone through one at a time.", once);
            });
        }

        [Fact]
        public void Real_julia_runs_them_alike_in_the_app_and_as_plain_julia()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                Loop loop = Design();

                // Plain Julia: the first high level's name shows once, the rows hold every file.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                using (JuliaSession session = new(julia.Path))
                {
                    ConcurrentQueue<OutputLine> output = new();
                    RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                    string printed = string.Join("\n", output.Select(l => l.Text));
                    Assert.True(result.Succeeded, printed);
                    Assert.Single(output, l => l.Text == "\"b.csv\"");
                    Assert.Contains(" 2.0\n 4.0", printed);
                }

                Assert.Equal("a.csv,1.0\nb.csv,2.0\nc.csv,4.0\n", File.ReadAllText(Rows).ReplaceLineEndings("\n"));

                // The app: the same, the rows started anew rather than added to the last run's.
                MainViewModel viewModel = new() { Julia = julia };
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                string log = string.Join("\n", viewModel.Output.Select(o => o.Text));
                Assert.Equal("b.csv", loop.FirstHigh.Result?.Text ?? log);
                Assert.Equal("b.csv", loop.First.Result?.Text ?? log);
                Assert.Equal("2-element Vector{Float64}:\n 2.0\n 4.0", loop.Last2.Result?.Text ?? log);
                Assert.Equal(ReportStatus.Ok, loop.Levels.Result?.Status);
                Assert.Equal("a.csv,1.0\nb.csv,2.0\nc.csv,4.0\n", File.ReadAllText(Rows).ReplaceLineEndings("\n"));
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Outside_a_loop_a_false_condition_says_why_nothing_after_it_ran()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                new MainViewModel().NewDesign();
                Float x = new() { Value = 1 };
                Function high = Define("high", "Bool", "x > 1.5", ("x", "Float32"));
                WhenNode when = new();
                DisplayNode shown = new();
                MainViewModel.Nodes.AddRange([x, high, when, shown]);
                Assert.Null(MainViewModel.Connect(x, high, high.Parameters[0]));
                Assert.Null(MainViewModel.Connect(x, when, when.Value));
                Assert.Null(MainViewModel.Connect(high, when, when.Condition));
                Assert.Null(MainViewModel.Connect(when, shown, shown.Input));

                MainViewModel viewModel = new() { Julia = julia };
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal((ReportStatus.Skipped, "The condition is false: nothing passes."), (when.Result?.Status, when.Result?.Text));
                Assert.Equal((ReportStatus.Skipped, "A condition before it is false: nothing passed."), (shown.Result?.Status, shown.Result?.Text));

                x.Value = 2;
                await viewModel.RunDesignAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("2.0f0", shown.Result?.Text);
                viewModel.EndJulia();
            });
        }
    }
}
