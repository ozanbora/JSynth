using JSynth.Model;
using JSynth.Runtime;
using JSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JSynth.Tests.Streams
{
    /// <summary>
    /// Live data: a web service as the sensor (fetched into the watched folder), its answers read as JSON, and
    /// only the newest of the files waiting gone through, for data where only the present matters.
    /// </summary>
    public sealed class LiveSourceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public LiveSourceTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// Answers with a counter as JSON, {"n": 1}, {"n": 2}, ..., until it has answered <paramref name="answers"/>
        /// times; then says it was asked too often (429).
        /// </summary>
        private static string Serve(int answers, CancellationToken cancellationToken)
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
                int n = 0;
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

                    if (++n > answers)
                    {
                        context.Response.StatusCode = 429;
                    }
                    else
                    {
                        byte[] body = Encoding.UTF8.GetBytes($"{{\"n\": {n}}}");
                        await context.Response.OutputStream.WriteAsync(body);
                    }

                    context.Response.Close();
                }
            }, cancellationToken);

            return $"http://localhost:{port}/states";
        }

        [Fact]
        public void A_watch_folder_fetching_names_its_files_by_the_time_and_makes_its_folder()
        {
            WatchNode watch = new() { Format = FileFormats.Json, Folder = Path.Combine(_folder, "live") };
            Assert.Equal("The folder doesn't exist.", watch.Error);

            watch.FetchUrl = "https://example.org/states";
            Assert.Null(watch.Error);
            Assert.Equal("The folder is made when the stream starts.", watch.Summary);
            Assert.Equal("2026-10-05T07-12-30.json", watch.FetchedName(new DateTime(2026, 10, 5, 7, 12, 30, DateTimeKind.Utc)));
            watch.Pattern = "states-*";
            Assert.Equal("2026-10-05T07-12-30.txt", watch.FetchedName(new DateTime(2026, 10, 5, 7, 12, 30, DateTimeKind.Utc)));

            // JSON holds a structure, not an array: its value is whatever it holds.
            Assert.Equal(Function.AnyType, watch.OutputType);
            Assert.Equal("JSON.parsefile(path)", FileFormats.Read(FileFormats.Json, "path", JuliaTypes.Scalar, "Float64", false, "A"));
            Assert.Equal(FileFormats.Json, FileFormats.OfExtension(".json"));
            Assert.Equal("write(path, JSON.json(v))", new SaveFile { Path = "path", Format = FileFormats.Json }.Statement("v").Replace("raw\"path\"", "path"));
        }

        [Fact]
        public void Fetching_saves_each_answer_as_a_new_file_and_stops_when_asked_too_often()
        {
            Ui.Run(async () =>
            {
                using CancellationTokenSource stop = new();
                string url = Serve(3, stop.Token);
                string into = Path.Combine(_folder, "live");
                MainViewModel viewModel = new();
                int named = 0;
                using HttpClient client = new();

                // Every 1 s: three answers, then 429, which ends the fetching.
                await viewModel.FetchAsync(client, url, into, 1, _ => $"{++named:000}.json", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

                Assert.Equal(["001.json", "002.json", "003.json"], Directory.GetFiles(into).Select(Path.GetFileName).Order());
                Assert.Equal("{\"n\": 3}", File.ReadAllText(Path.Combine(into, "003.json")));
                Assert.Contains(viewModel.Output, o => o.Text.StartsWith($"Stopped fetching: {url} says it was asked too often (429)."));
                stop.Cancel();
            });
        }

        [Fact]
        public void Real_julia_goes_through_only_the_newest_of_the_files_waiting_and_reads_json()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia
                    || (await new JuliaPackages(julia.Path).FindMissingAsync(["JSON"], _folder)).Count > 0)
                {
                    return;
                }

                // Three answers waiting at the start: with latest only, just the newest is gone through.
                string live = Path.Combine(_folder, "live");
                _ = Directory.CreateDirectory(live);
                foreach (int k in new[] { 1, 2, 3 })
                {
                    File.WriteAllText(Path.Combine(live, $"00{k}.json"), $"{{\"n\": {k}}}");
                }

                new MainViewModel().NewDesign();
                WatchNode watch = new() { Format = FileFormats.Json, Folder = live, LatestOnly = true };
                Function n = new() { Name = "count", Body = "J[\"n\"]" };
                n.Parameters.Add(new Parameter("J", Function.AnyType));
                MainViewModel.Nodes.AddRange([watch, n]);
                Assert.Null(MainViewModel.Connect(watch, n, n.Parameters[0]));
                Assert.Contains("; latest = true, stop = ", watch.Watch("stop"));

                string stop = Path.Combine(_folder, "stop");
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder, stop);
                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                Task<RunResult> running = session.RunAsync(script.Text, _folder, reported: Finished.Into(reports));
                Stopwatch clock = Stopwatch.StartNew();
                while (!reports.Any(r => r.Id == n.ID) && clock.Elapsed < TimeSpan.FromSeconds(60))
                {
                    await Task.Delay(50);
                }

                await Task.Delay(500);
                File.WriteAllText(stop, string.Empty);
                Assert.True((await running.WaitAsync(TimeSpan.FromSeconds(30))).Succeeded);
                Assert.Equal(["3"], reports.Where(r => r.Id == n.ID).Select(r => r.Text));
                Assert.Contains(reports, r => r.Id == watch.ID && r.Text == "1 file so far; the latest: 003.json");
            });
        }
    }
}
