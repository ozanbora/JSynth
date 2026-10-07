using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Waiting in a test for something another thread or a Julia process does.
    /// </summary>
    internal static class Wait
    {
        /// <summary>
        /// Looks at <paramref name="condition"/> every 50 ms until it holds or <paramref name="seconds"/> have gone by;
        /// whether it held.
        /// </summary>
        public static async Task<bool> Until(Func<bool> condition, double seconds)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                await Task.Delay(50);
            }

            return condition();
        }
    }
}
