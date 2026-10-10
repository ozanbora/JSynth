using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JSynth.Runtime
{
    /// <summary>
    /// A line a running script printed: to standard output or to standard error.
    /// </summary>
    public sealed record OutputLine(string Text, bool IsError);

    /// <summary>
    /// How a run ended.
    /// </summary>
    public sealed record RunResult(int ExitCode, bool Cancelled, TimeSpan Elapsed)
    {
        public bool Succeeded => !Cancelled && ExitCode == 0;
    }

    /// <summary>
    /// Runs Julia scripts with the Julia command-line program, one process per run.
    /// </summary>
    public sealed class JuliaRunner
    {
        private readonly string _executable;
        private readonly IReadOnlyList<string> _leadingArguments;

        /// <summary>
        /// A runner using the Julia program at <paramref name="juliaPath"/>.
        /// </summary>
        public JuliaRunner(string juliaPath) : this(juliaPath, [])
        {
        }

        /// <summary>
        /// A runner starting <paramref name="executable"/> with <paramref name="leadingArguments"/> before
        /// Julia's own; tests stand in for Julia this way.
        /// </summary>
        public JuliaRunner(string executable, IReadOnlyList<string> leadingArguments)
        {
            _executable = executable;
            _leadingArguments = leadingArguments;
        }

        /// <summary>
        /// Called for every line printed, except reports, on a background thread.
        /// </summary>
        public event Action<OutputLine>? Output;

        /// <summary>
        /// Called for every report (see <see cref="Reports"/>), on a background thread.
        /// </summary>
        public event Action<Report>? Reported;

        /// <summary>
        /// Saves <paramref name="script"/> to a temporary file and runs it in <paramref name="workingDirectory"/>
        /// (where relative paths in it start from). Cancelling kills Julia.
        /// </summary>
        public async Task<RunResult> RunAsync(string script, string workingDirectory, CancellationToken cancellationToken = default)
        {
            string folder = Path.Combine(Path.GetTempPath(), "JSynth");
            _ = Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"run-{Guid.NewGuid():N}.jl");
            await File.WriteAllTextAsync(file, script, new UTF8Encoding(false), cancellationToken);

            try
            {
                return await RunFileAsync(file, workingDirectory, cancellationToken);
            }
            finally
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Still open somewhere; the temp folder gets cleaned up eventually.
                }
            }
        }

        private async Task<RunResult> RunFileAsync(string file, string workingDirectory, CancellationToken cancellationToken)
        {
            ProcessStartInfo start = new(_executable)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (string argument in _leadingArguments)
            {
                start.ArgumentList.Add(argument);
            }

            // No startup.jl or REPL history, and no colour codes in the output; a thread a core, for work in parallel.
            foreach (string argument in new[] { "--startup-file=no", "--history-file=no", "--color=no", "--threads=auto", file })
            {
                start.ArgumentList.Add(argument);
            }

            Stopwatch clock = Stopwatch.StartNew();
            using Process process = new() { StartInfo = start };
            process.OutputDataReceived += (_, e) => Receive(e.Data, false);
            process.ErrorDataReceived += (_, e) => Receive(e.Data, true);

            _ = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It exited in the meantime.
                }

                await process.WaitForExitAsync(CancellationToken.None);
                return new RunResult(-1, true, clock.Elapsed);
            }

            // The exit can be seen before the last output is handled; this waits for the rest.
            process.WaitForExit();
            return new RunResult(process.ExitCode, false, clock.Elapsed);
        }

        private void Receive(string? line, bool isError)
        {
            if (line is null)
            {
                return;
            }

            if (!isError && Reports.Parse(line) is Report report)
            {
                Reported?.Invoke(report);
            }
            else
            {
                Output?.Invoke(new OutputLine(line, isError));
            }
        }
    }
}
