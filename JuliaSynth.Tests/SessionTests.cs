using JuliaSynth.Runtime;
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
    /// A Julia kept running between runs. Most of these need Julia, and pass without checking anything
    /// when it isn't installed.
    /// </summary>
    public sealed class SessionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public SessionTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// What one run printed and reported, and how it ended.
        /// </summary>
        private sealed record Outcome(RunResult Result, string[] Output, string[] Errors, Report[] Reports);

        private async Task<Outcome> Run(JuliaSession session, string script, CancellationToken cancellationToken = default)
        {
            ConcurrentQueue<OutputLine> lines = new();
            ConcurrentQueue<Report> reports = new();
            RunResult result = await session.RunAsync(script, _folder, lines.Enqueue, reports.Enqueue, cancellationToken);
            return new Outcome(
                result,
                lines.Where(l => !l.IsError).Select(l => l.Text).ToArray(),
                lines.Where(l => l.IsError).Select(l => l.Text).ToArray(),
                reports.ToArray());
        }

        private static async Task<JuliaSession?> Session()
        {
            return await JuliaLocator.FindAsync(null) is JuliaInstallation julia ? new JuliaSession(julia.Path) : null;
        }

        [Fact]
        public async Task Runs_report_in_one_julia_but_each_starts_without_the_names_of_the_last()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            string script = Reports.Prelude + "\n" + """
                println(@isdefined(seen) ? "seen before" : "new")
                seen = 2.5
                _jsynth_ok("a", seen * 2)
                println(pwd())
                """;

            Outcome first = await Run(session, script);
            Stopwatch clock = Stopwatch.StartNew();
            Outcome second = await Run(session, script);

            foreach (Outcome run in new[] { first, second })
            {
                Assert.True(run.Result.Succeeded);
                Assert.Equal(["new", Path.GetFullPath(_folder)], run.Output.Select(l => Path.EndsInDirectorySeparator(l) ? l[..^1] : l));
                Assert.Equal(new Report("a", ReportStatus.Ok, "Float64", "5.0"), Assert.Single(run.Reports));
            }

            // Julia was running already and the code compiled: no starting over.
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
        }

        [Fact]
        public async Task An_error_the_script_does_not_catch_fails_the_run_but_not_the_julia()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            Outcome failed = await Run(session, "print(\"half a line\")\nerror(\"broken on purpose\")\n");
            Outcome next = await Run(session, "println(1 + 1)");

            Assert.Equal(1, failed.Result.ExitCode);
            Assert.Contains("half a line", failed.Output);
            Assert.Contains(failed.Errors, l => l.Contains("broken on purpose"));
            Assert.True(next.Result.Succeeded);
            Assert.Equal(new[] { "2" }, next.Output);
        }

        [Fact]
        public async Task Stopping_ends_the_julia_and_the_next_run_gets_a_new_one()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            _ = await Run(session, "println(:warm)");
            using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));
            Stopwatch clock = Stopwatch.StartNew();

            Outcome stopped = await Run(session, "sleep(60)", cancellation.Token);
            Outcome next = await Run(session, "println(:again)");

            Assert.True(stopped.Result.Cancelled);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"took {clock.Elapsed}");
            Assert.True(next.Result.Succeeded);
            Assert.Equal(new[] { "again" }, next.Output);
        }

        [Fact]
        public async Task A_script_ending_julia_ends_its_run_with_julias_exit_code()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            Outcome exited = await Run(session, "println(:bye)\nexit(3)");
            Outcome next = await Run(session, "println(:back)");

            Assert.Equal(3, exited.Result.ExitCode);
            Assert.Equal(new[] { "bye" }, exited.Output);
            Assert.Equal(new[] { "back" }, next.Output);
        }

        [Fact]
        public async Task Names_from_the_script_show_as_in_a_julia_of_its_own()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            Outcome run = await Run(session, Reports.Prelude + "\n" + """
                struct Reading
                    celsius::Float64
                end
                _jsynth_ok("a", [Reading(12.5)])
                try
                    missing_name + 1
                catch e
                    _jsynth_error("b", e)
                end
                also_missing
                """);

            Report value = run.Reports.Single(r => r.Id == "a");
            Assert.Equal("Vector{Reading}", value.Type);
            Assert.Equal("1-element Vector{Reading}:\n Reading(12.5)", value.Text);
            Assert.Contains("not defined in `Main`", run.Reports.Single(r => r.Id == "b").Text);
            Assert.Contains(run.Errors, l => l.Contains("`also_missing` not defined in `Main`"));
            Assert.DoesNotContain("JuliaSynthRun", string.Join("\n", run.Errors.Concat(run.Reports.Select(r => r.Type + r.Text))));
        }

        [Fact]
        public async Task Warming_up_shows_nothing_and_the_runs_after_it_still_report()
        {
            using JuliaSession? session = await Session();
            if (session is null)
            {
                return;
            }

            await session.WarmUpAsync(JuliaSession.WarmUpScript(["LinearAlgebra", "NoSuchJuliaSynthPackage", "not a name"]));
            Outcome run = await Run(session, Reports.Prelude + "\n_jsynth_ok(\"b\", [1 2; 3 4])");

            Assert.True(run.Result.Succeeded);
            Assert.Equal("Matrix{Int64}", Assert.Single(run.Reports).Type);
        }

        [Fact]
        public async Task A_julia_that_ends_without_running_anything_ends_the_run_with_what_it_printed()
        {
            // The stand-in prints and exits whatever it is asked, like a Julia that crashed.
            using JuliaSession session = new(StandInJulia.Create(_folder, [$"{Reports.Marker}c\tok\tInt64\t7", "said", "!complained"], exitCode: 4));

            Outcome run = await Run(session, "println(1)");

            Assert.Equal(4, run.Result.ExitCode);
            Assert.Equal(["said"], run.Output.Select(l => l.Trim()));
            Assert.Equal(["complained"], run.Errors.Select(l => l.Trim()));
            Assert.Equal(new Report("c", ReportStatus.Ok, "Int64", "7"), Assert.Single(run.Reports));
        }
    }
}
