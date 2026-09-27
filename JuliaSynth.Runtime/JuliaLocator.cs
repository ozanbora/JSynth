using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.Runtime
{
    /// <summary>
    /// A Julia executable that answered <c>julia --version</c>.
    /// </summary>
    public sealed record JuliaInstallation(string Path, string Version);

    /// <summary>
    /// Finds the Julia command-line program: where the user said it is, else on the PATH, else where
    /// juliaup and the installers put it.
    /// </summary>
    public static class JuliaLocator
    {
        private static readonly Regex VersionRegex = new(@"julia version (\S+)", RegexOptions.IgnoreCase);

        // juliaup may install or update a Julia version the first time it runs.
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(60);

        private static string ExecutableName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "julia.exe" : "julia";

        /// <summary>
        /// The first place in <see cref="Candidates"/> holding a working Julia, or null.
        /// </summary>
        public static async Task<JuliaInstallation?> FindAsync(string? configuredPath, CancellationToken cancellationToken = default)
        {
            foreach (string path in Candidates(configuredPath))
            {
                if (await ProbeAsync(path, cancellationToken) is JuliaInstallation installation)
                {
                    return installation;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs <c>julia --version</c>; null when that isn't a working Julia.
        /// </summary>
        public static async Task<JuliaInstallation?> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            ProcessStartInfo start = new(path, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using Process process = Process.Start(start) ?? throw new InvalidOperationException();
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ProbeTimeout);

                Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw;
                }

                Match match = VersionRegex.Match(await output);
                return process.ExitCode == 0 && match.Success ? new JuliaInstallation(path, match.Groups[1].Value) : null;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
            {
                return null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out.
                return null;
            }
        }

        /// <summary>
        /// Where to look, most specific first; only paths that exist.
        /// </summary>
        public static IEnumerable<string> Candidates(string? configuredPath)
        {
            return Places(configuredPath).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> Places(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                yield return configuredPath;
            }

            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                if (folder.Trim().Trim('"') is { Length: > 0 } trimmed)
                {
                    yield return Path.Combine(trimmed, ExecutableName);
                }
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".juliaup", "bin", ExecutableName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                yield return Path.Combine(local, "Microsoft", "WindowsApps", ExecutableName);
                foreach (string root in new[] { Path.Combine(local, "Programs"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
                {
                    foreach (string folder in Newest(root, "Julia-*"))
                    {
                        yield return Path.Combine(folder, "bin", ExecutableName);
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                foreach (string app in Newest("/Applications", "Julia-*.app"))
                {
                    yield return Path.Combine(app, "Contents", "Resources", "julia", "bin", ExecutableName);
                }

                yield return "/opt/homebrew/bin/julia";
                yield return "/usr/local/bin/julia";
            }
            else
            {
                yield return "/usr/local/bin/julia";
                yield return "/usr/bin/julia";
            }
        }

        /// <summary>
        /// Folders matching <paramref name="pattern"/>, newest version first (Julia-1.11.2 before Julia-1.9.4).
        /// </summary>
        private static IEnumerable<string> Newest(string root, string pattern)
        {
            if (!Directory.Exists(root))
            {
                return [];
            }

            try
            {
                return Directory.GetDirectories(root, pattern)
                    .OrderByDescending(folder => Version.TryParse(Regex.Match(Path.GetFileName(folder), @"\d+(\.\d+)+").Value, out Version? v) ? v : new Version())
                    .ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }
}
