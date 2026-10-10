using JSynth.ViewModels;
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

namespace JSynth.Tests.Files
{
    /// <summary>
    /// Example data in an archive inside a large zip: only that entry is downloaded (range requests), and
    /// only the files wanted are kept. A local server stands in for NASA's, a tar archive for the RAR one.
    /// </summary>
    public sealed class ExampleArchiveTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

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
        public async Task An_archive_at_the_address_is_unpacked_its_files_renamed_and_converted_as_wanted()
        {
            // A video's archive: frames as JPEG pictures, their twins as PNG, and something else.
            using MemoryStream zip = new();
            using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach ((string name, byte[] data) in new[] { ("v/input/in1.jpg", Jpeg()), ("v/groundtruth/gt1.png", "marked"u8.ToArray()), ("v/ROI.bmp", new byte[10]) })
                {
                    using Stream entry = archive.CreateEntry(name).Open();
                    entry.Write(data);
                }
            }

            List<string> ranges = [];
            using CancellationTokenSource stop = new();
            string url = Serve(zip.ToArray(), ranges, stop.Token);
            using HttpClient client = new();
            ExampleArchive video = new(url, null)
            {
                Rename = key => key.Contains("/input/") || key.Contains("/groundtruth/") ? Path.ChangeExtension(Path.GetFileName(key), ".png") : null,
                Convert = (key, from, to) =>
                {
                    if (key.EndsWith(".jpg", StringComparison.Ordinal))
                    {
                        ExampleArchive.GreyPng(from, to);
                    }
                    else
                    {
                        from.CopyTo(to);
                    }
                },
            };

            int written = 0;
            await video.ExtractAsync(client, ["in1.png", "gt1.png"], _folder, () => written++, CancellationToken.None);

            Assert.Equal(2, written);
            Assert.Equal("marked", File.ReadAllText(Path.Combine(_folder, "gt1.png")));
            using SkiaSharp.SKBitmap grey = SkiaSharp.SKBitmap.Decode(Path.Combine(_folder, "in1.png"));
            Assert.Equal((4, 2, SkiaSharp.SKColorType.Gray8), (grey.Width, grey.Height, grey.ColorType));

            // The whole archive is downloaded, without ranges.
            Assert.Equal([string.Empty], ranges);
            stop.Cancel();
        }

        /// <summary>
        /// A 4×2 JPEG picture, all of one colour: 0.299 × 200 + 0.587 × 100 + 0.114 × 50 is a grey of 124.
        /// </summary>
        private static byte[] Jpeg()
        {
            using SkiaSharp.SKBitmap colour = new(4, 2);
            colour.Erase(new SkiaSharp.SKColor(200, 100, 50));
            using SkiaSharp.SKData jpeg = colour.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 100);
            return jpeg.ToArray();
        }

        [Fact]
        public void A_picture_turns_grey_as_julia_turns_colours_grey()
        {
            using MemoryStream from = new(Jpeg());
            using MemoryStream to = new();
            ExampleArchive.GreyPng(from, to);
            to.Position = 0;
            using SkiaSharp.SKBitmap grey = SkiaSharp.SKBitmap.Decode(to);

            // JPEG keeps a colour within a few levels.
            Assert.InRange(grey.GetPixel(1, 1).Red, 120, 128);
            Assert.Throws<InvalidDataException>(() => ExampleArchive.GreyPng(new MemoryStream("not a picture"u8.ToArray()), new MemoryStream()));
        }

        [Fact]
        public void The_highway_sets_are_frames_with_their_twins_from_the_videos_own_archive()
        {
            (ExampleDataSet shortSet, ExampleDataSet fullSet) = (Examples.CdnetHighway.Sets[0], Examples.CdnetHighway.Sets[1]);
            Assert.Equal((1600, 3400), (shortSet.Files.Count, fullSet.Files.Count));
            Assert.Equal(["in000001.png", "gt000001.png"], shortSet.Files.Take(2));
            Assert.Equal("gt001700.png", fullSet.Files[^1]);
            ExampleArchive archive = Examples.CdnetHighway.Archive!;
            Assert.Null(archive.Entry);
            Assert.Equal("in000123.png", archive.Rename!("highway/input/in000123.jpg"));
            Assert.Equal("gt000123.png", archive.Rename("highway/groundtruth/gt000123.png"));
            Assert.Null(archive.Rename("highway/ROI.bmp"));
            Assert.Null(archive.Rename("highway/temporalROI.txt"));
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
