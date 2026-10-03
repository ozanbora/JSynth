using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.Runtime
{
    /// <summary>
    /// A Julia kept running between runs, so code compiled for one run (and packages loaded) is still there
    /// for the next: a run takes a fraction of a second instead of starting Julia over each time.
    /// </summary>
    /// <remarks>
    /// Scripts run one at a time, each in a new module, so names from the last run don't carry over.
    /// Stopping a run ends that Julia and starts a new one; so does Julia ending (a crash, or a script
    /// calling <c>exit</c>), on the next run.
    /// </remarks>
    public sealed class JuliaSession : IDisposable
    {
        /// <summary>
        /// Ends each run, on both output streams, with the run's exit code: 0, or 1 for an error the
        /// script didn't catch.
        /// </summary>
        private const string DoneMarker = "\u001eJSYNTH-DONE\t";

        /// <summary>
        /// The loop Julia runs: read a script's path and folder, run it in a module of its own, mark the end.
        /// </summary>
        private const string Server = """
            let done = "\x1eJSYNTH-DONE\t"
                while !eof(stdin)
                    request = readline(stdin)
                    isempty(request) && continue
                    script, folder = split(request, '\t'; limit = 2)
                    code = 0
                    m = Module(:JuliaSynthRun)
                    try
                        cd(folder)
                        Base.include(m, script)
                    catch e
                        code = 1
                        bt = catch_backtrace()
                        shown = try
                            sprint(Base.display_error, e, bt)
                        catch
                            sprint(showerror, e) * "\n"
                        end
                        # As in a Julia of its own, where the script runs in Main.
                        name = string(m)
                        print(stderr, replace(shown, "$name." => "", "`$name`" => "`Main`", "@ $name " => "@ Main "))
                    finally
                        # Out of the run's folder, which Windows won't let go while a program is in it.
                        cd(tempdir())
                    end
                    # The run's values aren't needed any more: let them go, even if something still holds the module.
                    for n in names(m; all = true)
                        if isdefined(m, n) && !isconst(m, n)
                            try
                                Core.eval(m, :($n = nothing))
                            catch
                            end
                        end
                    end
                    flush(stdout)
                    flush(stderr)
                    println(stdout, done, code)
                    flush(stdout)
                    println(stderr, done, code)
                    flush(stderr)
                end
            end
            """;

        private readonly string _executable;
        private readonly IReadOnlyList<string> _leadingArguments;
        private readonly SemaphoreSlim _turn = new(1, 1);
        private Worker? _worker;
        private bool _disposed;

        /// <summary>
        /// A session with the Julia program at <paramref name="juliaPath"/>; Julia starts with the first run.
        /// </summary>
        public JuliaSession(string juliaPath) : this(juliaPath, [])
        {
        }

        /// <summary>
        /// A session starting <paramref name="executable"/> with <paramref name="leadingArguments"/> before
        /// Julia's own; tests stand in for Julia this way.
        /// </summary>
        public JuliaSession(string executable, IReadOnlyList<string> leadingArguments)
        {
            _executable = executable;
            _leadingArguments = leadingArguments;
        }

        /// <summary>
        /// Saves <paramref name="script"/> to a temporary file and runs it in <paramref name="workingDirectory"/>,
        /// after the runs before it. <paramref name="output"/> gets the lines printed, but reports, which go
        /// to <paramref name="reported"/>; both on a background thread. Cancelling ends this Julia (the next
        /// run starts a new one).
        /// </summary>
        public async Task<RunResult> RunAsync(string script, string workingDirectory, Action<OutputLine>? output = null, Action<Report>? reported = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                await _turn.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new RunResult(-1, true, TimeSpan.Zero);
            }

            // Ended while this run waited its turn: no Julia to start any more.
            if (_disposed)
            {
                _ = _turn.Release();
                return new RunResult(-1, true, TimeSpan.Zero);
            }

            string file = Path.Combine(Path.GetTempPath(), "JuliaSynth", $"run-{Guid.NewGuid():N}.jl");
            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllTextAsync(file, script, new UTF8Encoding(false), CancellationToken.None);
                return await RunFileAsync(file, workingDirectory, output, reported, cancellationToken);
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

                _ = _turn.Release();
            }
        }

        /// <summary>
        /// Starts Julia now, if it isn't running, and runs <paramref name="script"/> in it without showing
        /// anything: loading packages and compiling common code before the first run needs them.
        /// </summary>
        public Task WarmUpAsync(string script)
        {
            return _disposed ? Task.CompletedTask : RunAsync(script, Path.GetTempPath());
        }

        /// <summary>
        /// Julia code loading <paramref name="packages"/> (those that can be) and compiling how values are
        /// reported, for <see cref="WarmUpAsync"/>.
        /// </summary>
        public static string WarmUpScript(IEnumerable<string> packages)
        {
            StringBuilder script = new(Reports.Prelude.TrimEnd() + "\n");
            foreach (string package in packages.Distinct().Where(JuliaPackages.IsName))
            {
                _ = script.Append($"try\n    Core.eval(@__MODULE__, :(import {package}))\ncatch\nend\n");
            }

            // Shown the way runs show values, to a stream nobody reads.
            _ = script.Append("""
                redirect_stdout(devnull) do
                    for v in (1.5, 3, UInt64(2), true, "text", [1.0, 2.0], [1 2; 3 4], rand(3, 3), 1:3, (1, 2.0))
                        _jsynth_ok("warm", v)
                    end
                    _jsynth_error("warm", ErrorException("warm"))
                end

                """);
            return script.ToString();
        }

        /// <summary>
        /// Ends this Julia; the next run starts a new one, without anything earlier runs left behind.
        /// </summary>
        public void Restart()
        {
            Worker? worker = Interlocked.Exchange(ref _worker, null);
            worker?.Kill();
        }

        public void Dispose()
        {
            _disposed = true;
            Restart();
        }

        private async Task<RunResult> RunFileAsync(string file, string workingDirectory, Action<OutputLine>? output, Action<Report>? reported, CancellationToken cancellationToken)
        {
            Stopwatch clock = Stopwatch.StartNew();
            Run run = new(output, reported);
            Worker worker;
            if (_worker is { HasExited: false } running)
            {
                worker = running;
                worker.Begin(run);
            }
            else
            {
                worker = Start(run);
            }

            await worker.SendAsync($"{file}\t{workingDirectory}");

            bool cancelled = false;
            using (cancellationToken.Register(() =>
            {
                if (!run.Done.Task.IsCompleted)
                {
                    cancelled = true;
                    worker.Kill();
                }
            }))
            {
                int exitCode = await run.Done.Task;
                worker.End();

                // A Julia that ended (stopped, crashed, or a script exiting) is replaced on the next run.
                if (worker.HasExited)
                {
                    _ = Interlocked.CompareExchange(ref _worker, null, worker);
                }

                return new RunResult(cancelled ? -1 : exitCode, cancelled, clock.Elapsed);
            }
        }

        private Worker Start(Run run)
        {
            ProcessStartInfo start = new(_executable)
            {
                // Between runs, Julia waits in a folder nobody minds it holding on to.
                WorkingDirectory = Path.GetTempPath(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (string argument in _leadingArguments)
            {
                start.ArgumentList.Add(argument);
            }

            // As for a run in a Julia of its own: no startup.jl, REPL history or colour codes.
            foreach (string argument in new[] { "--startup-file=no", "--history-file=no", "--color=no", "-e", Server })
            {
                start.ArgumentList.Add(argument);
            }

            Worker worker = new(start, run);
            _worker = worker;
            return worker;
        }

        /// <summary>
        /// One run: where its output goes, and its exit code once both streams said it ended (or Julia did).
        /// </summary>
        private sealed class Run(Action<OutputLine>? output, Action<Report>? reported)
        {
            private int _streamsDone;

            public TaskCompletionSource<int> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Receive(string line, bool isError)
            {
                if (!isError && Reports.Parse(line) is Report report)
                {
                    reported?.Invoke(report);
                }
                else
                {
                    output?.Invoke(new OutputLine(line, isError));
                }
            }

            /// <summary>
            /// One stream ended the run; the run is over once both did.
            /// </summary>
            public void StreamDone(int exitCode)
            {
                if (Interlocked.Increment(ref _streamsDone) == 2)
                {
                    _ = Done.TrySetResult(exitCode);
                }
            }
        }

        /// <summary>
        /// The Julia process, and the run it is busy with.
        /// </summary>
        private sealed class Worker
        {
            private readonly Process _process;
            private Run? _run;

            public Worker(ProcessStartInfo start, Run run)
            {
                _run = run;
                _process = new Process { StartInfo = start, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) => Receive(e.Data, false);
                _process.ErrorDataReceived += (_, e) => Receive(e.Data, true);
                _process.Exited += (_, _) => _ = Task.Run(Ended);
                _ = _process.Start();
                _process.StandardInput.NewLine = "\n";
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            public bool HasExited
            {
                get
                {
                    try
                    {
                        return _process.HasExited;
                    }
                    catch (InvalidOperationException)
                    {
                        return true;
                    }
                }
            }

            public void Begin(Run run)
            {
                Volatile.Write(ref _run, run);

                // Julia ended before the run began: nothing will mark its end.
                if (HasExited)
                {
                    _ = Task.Run(Ended);
                }
            }

            public void End()
            {
                Volatile.Write(ref _run, null);
            }

            /// <summary>
            /// Asks Julia to run a script; if it ended already, the run ends with it.
            /// </summary>
            public async Task SendAsync(string request)
            {
                try
                {
                    await _process.StandardInput.WriteLineAsync(request);
                    await _process.StandardInput.FlushAsync();
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    // Gone; Ended finishes the run.
                }
            }

            public void Kill()
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Ended already.
                }
            }

            private void Receive(string? line, bool isError)
            {
                Run? run = Volatile.Read(ref _run);
                if (line is null || run is null)
                {
                    return;
                }

                // The end of a run, perhaps after output that didn't end its line.
                int marker = line.IndexOf(DoneMarker, StringComparison.Ordinal);
                if (marker >= 0 && int.TryParse(line.AsSpan(marker + DoneMarker.Length), out int exitCode))
                {
                    if (marker > 0)
                    {
                        run.Receive(line[..marker], isError);
                    }

                    run.StreamDone(exitCode);
                    return;
                }

                run.Receive(line, isError);
            }

            private void Ended()
            {
                try
                {
                    // Lets the output still on its way be handled first.
                    _process.WaitForExit();
                }
                catch (InvalidOperationException)
                {
                }

                if (Volatile.Read(ref _run) is Run run)
                {
                    int exitCode;
                    try
                    {
                        exitCode = _process.ExitCode;
                    }
                    catch (InvalidOperationException)
                    {
                        exitCode = -1;
                    }

                    // Julia ending without saying the run ended: a crash, or exit() in the script.
                    _ = run.Done.TrySetResult(exitCode);
                }
            }
        }
    }
}
