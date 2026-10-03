using System;
using System.IO;

namespace JuliaSynth.Desktop
{
    /// <summary>
    /// Windows Subsystem for Linux, where WSLg shows the app's windows on the Windows desktop.
    /// </summary>
    public static class Wsl
    {
        /// <summary>
        /// Whether the app runs in a WSL distribution: its kernel says so.
        /// </summary>
        public static bool IsRunningUnder { get; } = Detect();

        private static bool Detect()
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            if (Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is { Length: > 0 })
            {
                return true;
            }

            try
            {
                return File.ReadAllText("/proc/sys/kernel/osrelease").Contains("microsoft", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
