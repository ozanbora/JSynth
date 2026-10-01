using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The Julia runtime. Julia itself stands in as small shell scripts, so these run without it; the
    /// last test runs the real thing when Julia is installed.
    /// </summary>
    public sealed class RuntimeTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public RuntimeTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private string FakeJulia(string[] lines, int exitCode = 0, int sleepSeconds = 0)
        {
            return StandInJulia.Create(_folder, lines, exitCode, sleepSeconds);
        }

        [Fact]
        public void Reports_are_read_back_with_escapes_undone()
        {
            Report? report = Reports.Parse($"{Reports.Marker}abc\tok\tVector{{Float64}}\t3-element Vector{{Float64}}:\\n 1.0\\n a\\\\b\\tc");

            Assert.Equal(new Report("abc", ReportStatus.Ok, "Vector{Float64}", "3-element Vector{Float64}:\n 1.0\n a\\b\tc"), report);
            Assert.Null(Reports.Parse("Hello from Julia"));
            Assert.Null(Reports.Parse($"{Reports.Marker}abc\tmaybe\t\t"));
        }

        [Fact]
        public async Task A_working_julia_answers_with_its_version()
        {
            string julia = FakeJulia(["julia version 1.11.2"]);

            JuliaInstallation? installation = await JuliaLocator.ProbeAsync(julia);

            Assert.Equal(new JuliaInstallation(julia, "1.11.2"), installation);
            Assert.Null(await JuliaLocator.ProbeAsync(FakeJulia(["something else"])));
            Assert.Null(await JuliaLocator.ProbeAsync(Path.Combine(_folder, "missing.exe")));
        }

        [Fact]
        public void The_julia_the_user_picked_is_tried_first()
        {
            string julia = FakeJulia(["julia version 1.11.2"]);

            Assert.Equal(julia, JuliaLocator.Candidates(julia).First());
            Assert.DoesNotContain(Path.Combine(_folder, "missing.exe"), JuliaLocator.Candidates(Path.Combine(_folder, "missing.exe")));
        }

        [Fact]
        public async Task A_run_reports_values_and_passes_other_output_on()
        {
            string julia = FakeJulia([$"{Reports.Marker}id1\tok\tFloat64\t1.5", "hello", "!a warning"], exitCode: 3);
            JuliaRunner runner = new(julia);
            ConcurrentQueue<Report> reports = new();
            ConcurrentQueue<OutputLine> output = new();
            runner.Reported += reports.Enqueue;
            runner.Output += output.Enqueue;

            RunResult result = await runner.RunAsync("println(1)", _folder);

            Assert.Equal(3, result.ExitCode);
            Assert.False(result.Succeeded);
            Assert.Equal(new Report("id1", ReportStatus.Ok, "Float64", "1.5"), Assert.Single(reports));
            Assert.Contains(new OutputLine("hello", false), output);
            Assert.Contains(output, line => line.IsError && line.Text.Trim() == "a warning");
        }

        [Fact]
        public async Task Stopping_a_run_kills_julia()
        {
            JuliaRunner runner = new(FakeJulia(["started"], sleepSeconds: 30));
            using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));
            Stopwatch clock = Stopwatch.StartNew();

            RunResult result = await runner.RunAsync("sleep(30)", _folder, cancellation.Token);

            Assert.True(result.Cancelled);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"took {clock.Elapsed}");
        }

        [Fact]
        public void The_starting_design_exports_as_plain_julia()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();

                string script = MainViewModel.ExportScript();

                Assert.Contains("function f(x::Float64, z::Int64)::Float64\n    x ^ z\nend", script);
                Assert.Contains("x1 = Float64(1.5)\nz1 = Int64(3)\nn1 = UInt64(2)\nf_out = f(x1, z1)\ng_out = g(f_out, n1)\n", script);
                Assert.DoesNotContain("_jsynth", script);
            });
        }

        [Fact]
        public void A_run_script_reports_every_node_and_guards_the_dependent_ones()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function g = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "g");
                Function f = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "f");

                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);

                Assert.StartsWith("# Generated by JuliaSynth.", script.Text);
                Assert.Contains(Reports.Prelude.TrimEnd(), script.Text);
                Assert.Equal(5, MainViewModel.Nodes.Count(n => script.Text.Contains($"_jsynth_ok({ScriptGenerator.StringLiteral(n.ID)}, {script.Variables[n.ID]})")));
                Assert.Contains($"include_string(Main, \"function g(y::Float64, n::UInt64)::Float64\\n    y / n\\nend\", \"g\")", script.Text);

                // g runs only when its definition, f's value and n's value came out fine.
                Assert.Contains($"elseif _jsynth_blocked(\"{f.ID}\", ", script.Text);
                Assert.Contains($"if _jsynth_blocked(\"{g.ID}\")\n    nothing\n", script.Text);
            });
        }

        [Fact]
        public void Nodes_that_cannot_run_are_skipped_with_the_reason()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function g = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "g");
                MainViewModel.Disconnect(MainViewModel.ConnectionTo(g.Parameters[1])!);

                string plain = MainViewModel.ExportScript();
                string run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;

                Assert.Contains("# g_out: Parameter n isn't wired.", plain);
                Assert.Contains($"_jsynth_skip(\"{g.ID}\", \"Parameter n isn't wired.\")", run);
            });
        }

        [Fact]
        public void Wired_builder_arguments_are_bound_and_packages_loaded_one_by_one()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Integer z = MainViewModel.Nodes.OfType<Integer>().Single();
                ArrayBuilder builder = new() { Kind = "sprand", Shape = "Matrix" };
                ArrayFile file = new() { Path = "data/a.mtx" };
                MainViewModel.Nodes.AddRange([builder, file]);
                Assert.Null(MainViewModel.Connect(z, builder, builder.Parameters[0]));

                string plain = MainViewModel.ExportScript();
                string run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;

                Assert.Contains("using MatrixMarket, SparseArrays\n", plain);
                Assert.Contains("B1 = let m = z1\n    sprand(Float64, m, 100, 0.01)\nend\n", plain);
                Assert.Contains("D1 = SparseMatrixCSC{Float64, Int64}(MatrixMarket.mmread(raw\"data/a.mtx\"))", plain);
                Assert.Contains("for (_p, _how) in ((\"MatrixMarket\", \"using\"), (\"SparseArrays\", \"using\"),)", run);
                Assert.Contains("_jsynth_package_problem(\"MatrixMarket\", \"SparseArrays\")", run);
            });
        }

        [Fact]
        public void Variables_never_clash_with_function_names()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel.Nodes.OfType<Function>().First().Name = "x1";

                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false);

                Assert.Equal(script.Variables.Count, script.Variables.Values.Distinct().Count());
                Assert.DoesNotContain("x1", script.Variables.Values);
            });
        }

        [Theory]
        [InlineData("a\"b", "\"a\\\"b\"")]
        [InlineData("cost: $5\\n", "\"cost: \\$5\\\\n\"")]
        [InlineData("line\r\nnext", "\"line\\nnext\"")]
        public void Text_becomes_an_exact_julia_string(string text, string literal)
        {
            Assert.Equal(literal, ScriptGenerator.StringLiteral(text));
        }

        [Fact]
        public void Changing_the_design_clears_the_results_of_the_last_run()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Float x = MainViewModel.Nodes.OfType<Float>().Single();
                x.Result = new Report(x.ID, ReportStatus.Ok, "Float64", "1.5");
                Assert.True(x.HasResult);
                Assert.Equal("Float64\n1.5", x.ResultTip);

                x.IsSelected = true;
                Assert.True(x.HasResult);

                x.Value = 2.5;
                Assert.False(x.HasResult);
            });
        }

        [Fact]
        public async Task The_starting_design_runs_with_real_julia_when_installed()
        {
            // Nothing to check against without Julia; the fakes above cover the rest.
            if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
            {
                return;
            }

            string script = string.Empty;
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;
            });

            JuliaRunner runner = new(julia.Path);
            ConcurrentQueue<Report> reports = new();
            runner.Reported += reports.Enqueue;
            RunResult result = await runner.RunAsync(script, _folder);

            Assert.True(result.Succeeded);
            Assert.Equal(5, reports.Count);
            Assert.All(reports, r => Assert.Equal(ReportStatus.Ok, r.Status));
            Assert.Contains(reports, r => r.Type == "Float64" && r.Text == "1.6875");
        }
    }
}
