using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Example data in an archive inside a large zip: only that entry is downloaded (range requests), and
    /// only the files wanted are kept. A local server stands in for NASA's, a tar archive for the RAR one.
    /// </summary>
    public sealed class ExampleArchiveTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public ExampleArchiveTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// A zip of a readme, a tar archive of three snapshots in a folder, and something else after them.
        /// </summary>
        private static byte[] Zip()
        {
            using MemoryStream tar = new();
            using (TarWriter writer = new(tar, leaveOpen: true))
            {
                foreach (string name in new[] { "a", "b", "c" })
                {
                    byte[] data = Encoding.ASCII.GetBytes($"{name}\t1.0\n");
                    writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, $"test/{name}") { DataStream = new MemoryStream(data) });
                }
            }

            using MemoryStream zip = new();
            using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach ((string name, byte[] data) in new[] { ("X/readme.txt", Encoding.ASCII.GetBytes("about")), ("X/test.tar", tar.ToArray()), ("X/other.bin", new byte[5000]) })
                {
                    using Stream entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                    entry.Write(data);
                }
            }

            return zip.ToArray();
        }

        /// <summary>
        /// Serves <paramref name="body"/> at a local address, answering range requests, until cancelled; the
        /// ranges asked for are noted.
        /// </summary>
        private static string Serve(byte[] body, List<string> ranges, CancellationToken cancellationToken)
        {
            TcpListener free = new(IPAddress.Loopback, 0);
            free.Start();
            int port = ((IPEndPoint)free.LocalEndpoint).Port;
            free.Stop();

            HttpListener listener = new();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            _ = cancellationToken.Register(listener.Stop);
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await listener.GetContextAsync();
                    }
                    catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
                    {
                        return;
                    }

                    string range = context.Request.Headers["Range"] ?? string.Empty;
                    lock (ranges)
                    {
                        ranges.Add(range);
                    }

                    (long from, long to) = (0, body.Length - 1);
                    if (range.StartsWith("bytes=", StringComparison.Ordinal))
                    {
                        string[] ends = range["bytes=".Length..].Split('-');
                        (from, to) = ends[0].Length == 0
                            ? (Math.Max(0, body.Length - long.Parse(ends[1])), body.Length - 1)
                            : (long.Parse(ends[0]), Math.Min(body.Length - 1, ends[1].Length == 0 ? body.Length - 1 : long.Parse(ends[1])));
                        context.Response.StatusCode = 206;
                        context.Response.Headers["Content-Range"] = $"bytes {from}-{to}/{body.Length}";
                    }

                    context.Response.ContentLength64 = to - from + 1;
                    await context.Response.OutputStream.WriteAsync(body.AsMemory((int)from, (int)(to - from + 1)));
                    context.Response.Close();
                }
            }, cancellationToken);

            return $"http://localhost:{port}/IMS.zip";
        }

        [Fact]
        public async Task Only_the_archive_is_downloaded_and_only_the_files_wanted_are_kept()
        {
            byte[] zip = Zip();
            List<string> ranges = [];
            using CancellationTokenSource stop = new();
            string url = Serve(zip, ranges, stop.Token);
            using HttpClient client = new();
            int written = 0;

            await new ExampleArchive(url, "X/test.tar").ExtractAsync(client, ["a", "c"], _folder, () => written++, CancellationToken.None);

            Assert.Equal(2, written);
            Assert.Equal(["a", "c"], Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
            Assert.Equal("c\t1.0\n", File.ReadAllText(Path.Combine(_folder, "c")));

            // The zip's list of entries from its end, then the archive's entry: never the whole zip.
            Assert.Equal("bytes=-65536", ranges[0]);
            Assert.All(ranges, r => Assert.StartsWith("bytes=", r));

            // Files there already are kept, and counted; a file the archive hasn't is said.
            File.WriteAllText(Path.Combine(_folder, "a"), "kept");
            written = 0;
            await new ExampleArchive(url, "X/test.tar").ExtractAsync(client, ["a", "b"], _folder, () => written++, CancellationToken.None);
            Assert.Equal((2, "kept"), (written, File.ReadAllText(Path.Combine(_folder, "a"))));

            InvalidDataException missing = await Assert.ThrowsAsync<InvalidDataException>(
                () => new ExampleArchive(url, "X/test.tar").ExtractAsync(client, ["a", "z"], _folder, () => { }, CancellationToken.None));
            Assert.Equal("X/test.tar has no z.", missing.Message);
            InvalidDataException noEntry = await Assert.ThrowsAsync<InvalidDataException>(
                () => new ExampleArchive(url, "X/none.rar").ExtractAsync(client, ["a"], _folder, () => { }, CancellationToken.None));
            Assert.Equal($"The zip at {url} has no X/none.rar.", noEntry.Message);
            stop.Cancel();
        }

        [Fact]
        public void The_bearing_sets_are_snapshots_every_10_minutes_the_short_one_every_8th_and_the_last()
        {
            (ExampleDataSet shortSet, ExampleDataSet fullSet) = (Examples.ImsBearings.Sets[0], Examples.ImsBearings.Sets[1]);
            Assert.Equal((124, 984), (shortSet.Files.Count, fullSet.Files.Count));
            Assert.Equal(("2004.02.12.10.32.39", "2004.02.19.06.22.39"), (fullSet.Files[0], fullSet.Files[^1]));
            Assert.Equal(["2004.02.12.10.32.39", "2004.02.12.11.52.39"], shortSet.Files.Take(2));
            Assert.Equal(fullSet.Files[^1], shortSet.Files[^1]);
            Assert.Subset(fullSet.Files.ToHashSet(), shortSet.Files.ToHashSet());
            Assert.Equal(new ExampleArchive("https://data.nasa.gov/docs/legacy/IMS.zip", "IMS/2nd_test.rar"), Examples.ImsBearings.Archive);
        }
    }
}
