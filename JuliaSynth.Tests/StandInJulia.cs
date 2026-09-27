using System;
using System.IO;
using System.Linq;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Small shell scripts standing in for the Julia program, so the runtime can be tested without Julia.
    /// </summary>
    public static class StandInJulia
    {
        /// <summary>
        /// A program in <paramref name="folder"/> printing <paramref name="lines"/> (a line starting with "!"
        /// goes to standard error) and exiting with <paramref name="exitCode"/>, after <paramref name="sleepSeconds"/>.
        /// It ignores its arguments, so it answers every call the same way.
        /// </summary>
        public static string Create(string folder, string[] lines, int exitCode = 0, int sleepSeconds = 0)
        {
            bool windows = OperatingSystem.IsWindows();
            string path = Path.Combine(folder, $"julia-{Guid.NewGuid():N}{(windows ? ".cmd" : string.Empty)}");
            string Echo(string line) => windows
                ? (line.StartsWith('!') ? $"echo {line[1..]} 1>&2" : $"echo {line}")
                : (line.StartsWith('!') ? $"printf '%s\\n' '{line[1..]}' 1>&2" : $"printf '%s\\n' '{line}'");

            string sleep = sleepSeconds == 0 ? string.Empty
                : windows ? $"ping -n {sleepSeconds + 1} 127.0.0.1 >nul" : $"sleep {sleepSeconds}";
            string script = windows
                ? string.Join("\r\n", ["@echo off", .. lines.Select(Echo), sleep, $"exit /b {exitCode}"])
                : string.Join("\n", ["#!/bin/sh", .. lines.Select(Echo), sleep, $"exit {exitCode}"]);
            File.WriteAllText(path, script);
            if (!windows)
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return path;
        }
    }
}
