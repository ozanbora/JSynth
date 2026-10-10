using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JSynth.Runtime
{
    /// <summary>
    /// Which Julia packages can be loaded, and installing the ones that can't, with Julia's package manager.
    /// </summary>
    /// <remarks>
    /// Both run Julia in a folder, like a script: that decides which environment the packages are looked up
    /// in and added to (normally the default one, shared by all scripts).
    /// </remarks>
    public sealed class JuliaPackages
    {
        private const string MissingMarker = "\u001eJSYNTH-MISSING\t";

        // Package names are Julia identifiers; nothing else may end up in the generated code.
        private static readonly Regex NameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

        private readonly Func<JuliaRunner> _newRunner;
        private readonly JuliaSession? _session;

        public JuliaPackages(string juliaPath) : this(() => new JuliaRunner(juliaPath))
        {
        }

        /// <summary>
        /// Package handling that checks in <paramref name="session"/>, a Julia already running, and installs
        /// with a Julia of its own: the package manager is better kept out of the Julia running designs.
        /// </summary>
        public JuliaPackages(string juliaPath, JuliaSession session) : this(juliaPath)
        {
            _session = session;
        }

        /// <summary>
        /// Package handling running Julia through <paramref name="newRunner"/>; tests stand in for Julia this way.
        /// </summary>
        public JuliaPackages(Func<JuliaRunner> newRunner)
        {
            _newRunner = newRunner;
        }

        /// <summary>
        /// Julia code printing a marker line for each of <paramref name="names"/> that can't be loaded.
        /// Standard libraries (SparseArrays, LinearAlgebra, ...) are always found.
        /// </summary>
        public static string CheckScript(IEnumerable<string> names)
        {
            return $"for name in [{Quoted(names)}]\n"
                + "    Base.find_package(name) === nothing && println(\"\\x1eJSYNTH-MISSING\\t\", name)\n"
                + "end\n";
        }

        /// <summary>
        /// Julia code adding <paramref name="names"/> to the active environment.
        /// </summary>
        public static string InstallScript(IEnumerable<string> names)
        {
            return $"import Pkg\nPkg.add([{Quoted(names)}])\n";
        }

        /// <summary>
        /// Those of <paramref name="names"/> that Julia can't load, in the order given.
        /// </summary>
        /// <exception cref="InvalidOperationException">Julia failed to check.</exception>
        public async Task<IReadOnlyList<string>> FindMissingAsync(IEnumerable<string> names, string workingDirectory, CancellationToken cancellationToken = default)
        {
            List<string> wanted = Valid(names);
            if (wanted.Count == 0)
            {
                return [];
            }

            List<string> missing = [];
            List<string> errors = [];
            void Receive(OutputLine line)
            {
                lock (missing)
                {
                    if (!line.IsError && line.Text.StartsWith(MissingMarker, StringComparison.Ordinal))
                    {
                        missing.Add(line.Text[MissingMarker.Length..].Trim());
                    }
                    else if (line.IsError)
                    {
                        errors.Add(line.Text);
                    }
                }
            }

            RunResult result;
            if (_session is null)
            {
                JuliaRunner runner = _newRunner();
                runner.Output += Receive;
                result = await runner.RunAsync(CheckScript(wanted), workingDirectory, cancellationToken);
            }
            else
            {
                result = await _session.RunAsync(CheckScript(wanted), workingDirectory, Receive, cancellationToken: cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Julia couldn't check the packages: {string.Join(" ", errors).Trim()}");
            }

            lock (missing)
            {
                return wanted.Where(missing.Contains).ToList();
            }
        }

        /// <summary>
        /// Installs <paramref name="names"/>, passing on what the package manager prints (downloads,
        /// precompiling, ...), mostly on standard error.
        /// </summary>
        public async Task<RunResult> InstallAsync(IEnumerable<string> names, string workingDirectory, Action<OutputLine>? output = null, CancellationToken cancellationToken = default)
        {
            List<string> wanted = Valid(names);
            if (wanted.Count == 0)
            {
                return new RunResult(0, false, TimeSpan.Zero);
            }

            JuliaRunner runner = _newRunner();
            if (output is not null)
            {
                runner.Output += output;
            }

            return await runner.RunAsync(InstallScript(wanted), workingDirectory, cancellationToken);
        }

        /// <summary>
        /// Whether <paramref name="name"/> can be a package name: a Julia identifier.
        /// </summary>
        public static bool IsName(string name)
        {
            return NameRegex.IsMatch(name);
        }

        private static List<string> Valid(IEnumerable<string> names)
        {
            List<string> valid = names.Distinct().ToList();
            return valid.FirstOrDefault(n => !NameRegex.IsMatch(n)) is string invalid
                ? throw new ArgumentException($"'{invalid}' is not a Julia package name.", nameof(names))
                : valid;
        }

        private static string Quoted(IEnumerable<string> names)
        {
            return string.Join(", ", Valid(names).Select(n => $"\"{n}\""));
        }
    }
}
