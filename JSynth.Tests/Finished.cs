using JSynth.Runtime;
using System;
using System.Collections.Concurrent;

namespace JSynth.Tests
{
    /// <summary>
    /// The reports of values worked out, failed or skipped, without those saying a value is being worked out.
    /// </summary>
    internal static class Finished
    {
        public static Action<Report> Into(ConcurrentQueue<Report> reports) => report =>
        {
            if (report.Status != ReportStatus.Running)
            {
                reports.Enqueue(report);
            }
        };
    }
}
