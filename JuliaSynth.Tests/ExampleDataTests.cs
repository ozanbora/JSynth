using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Data of examples that is downloaded when they open rather than bundled. The internet is played by a
    /// stand-in; the last test runs the radar example on the real scans when they were downloaded before.
    /// </summary>
    public sealed class ExampleDataTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;
        private readonly HttpClient _originalHttp = MainViewModel.Http;

        public ExampleDataTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            MainViewModel.Http = _originalHttp;
            Directory.Delete(_folder, recursive: true);
        }

        private static readonly ExampleDataSet Few = new("Short", "One scan.", ["a.png"], 1_000);

        private static readonly ExampleDataSet All = new("Full", "Two scans.", ["a.png", "b.png"], 2_000);

        private static readonly ExampleData Data = new(
            "Test scans", "Scans.", "By nobody, for tests.", "https://example.invalid/scans/", [Few, All]);

        /// <summary>
        /// Answers every request with the file's name as its content, or 404 for the names in <paramref name="missing"/>.
        /// </summary>
        private sealed class StandInInternet(params string[] missing) : HttpMessageHandler
        {
            public ConcurrentQueue<string> Asked { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string name = request.RequestUri!.Segments[^1];
                Asked.Enqueue(name);
                return Task.FromResult(missing.Contains(name)
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"scan {name}") });
            }
        }

        [Fact]
        public async Task Downloading_fetches_the_missing_files_credits_them_and_remembers_the_folder()
        {
            string target = Path.Combine(_folder, "data");
            string full = Path.Combine(target, "full");
            _ = Directory.CreateDirectory(full);
            File.WriteAllText(Path.Combine(full, "a.png"), "already here");
            StandInInternet internet = new();
            List<int> progress = [];
            Assert.Null(Data.DownloadedFolder);

            await Data.DownloadAsync(new HttpClient(internet), target, All, progress.Add);

            Assert.Equal(["b.png"], internet.Asked);
            Assert.Equal("already here", File.ReadAllText(Path.Combine(full, "a.png")));
            Assert.Equal("scan b.png", File.ReadAllText(Path.Combine(full, "b.png")));
            Assert.Contains("By nobody, for tests.", File.ReadAllText(Path.Combine(target, ExampleData.ReadmeName)));
            Assert.Equal([1, 2], progress);
            Assert.True(ExampleData.IsIn(target, All));
            Assert.False(ExampleData.IsIn(target, Few));
            Assert.Equal(target, Data.Folder);
            Assert.Equal(full, Data.DownloadedFolder);
            Assert.Empty(Directory.GetFiles(full, "*.part"));
        }

        [Fact]
        public async Task Each_set_goes_in_a_folder_of_its_own_and_the_largest_one_there_is_used()
        {
            string target = Path.Combine(_folder, "data");

            await Data.DownloadAsync(new HttpClient(new StandInInternet()), target, Few);
            Assert.Equal(Path.Combine(target, "short"), Data.DownloadedFolder);
            Assert.Equal(["a.png"], Directory.GetFiles(Path.Combine(target, "short")).Select(Path.GetFileName));

            await Data.DownloadAsync(new HttpClient(new StandInInternet()), target, All);
            Assert.Equal(Path.Combine(target, "full"), Data.DownloadedFolder);
            Assert.Equal("Short (1 files, 1 kB)", Few.Label);
            Assert.Equal("Full (2 files, 2 kB)", All.Label);
        }

        [Fact]
        public async Task A_file_that_cannot_be_downloaded_stops_it_and_the_folder_is_not_remembered()
        {
            string target = Path.Combine(_folder, "data");

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => Data.DownloadAsync(new HttpClient(new StandInInternet("b.png")), target, All));

            Assert.True(File.Exists(Path.Combine(target, "full", "a.png")));
            Assert.False(File.Exists(Path.Combine(target, "full", "b.png")));
            Assert.False(ExampleData.IsIn(target, All));
            Assert.Equal(Data.DefaultFolder, Data.Folder);
        }

        [Fact]
        public void The_window_shows_the_download_going_and_says_how_it_ended()
        {
            Ui.Run(async () =>
            {
                MainViewModel viewModel = new();
                List<string> seen = [];
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.DownloadText) && viewModel.IsDownloading)
                    {
                        seen.Add(viewModel.DownloadText);
                    }
                };

                MainViewModel.Http = new HttpClient(new StandInInternet());
                Assert.True(await viewModel.DownloadExampleDataAsync(Data, All, Path.Combine(_folder, "ok")));

                // Anything still queued for the window runs first: the banner must stay gone after it.
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(seen, text => text.StartsWith("Downloading Test scans (full): 0 of 2 files."));
                Assert.False(viewModel.IsDownloading);
                Assert.False(viewModel.IsRunning);
                Assert.Contains(viewModel.Output, o => o.Text.StartsWith("Downloaded Test scans to "));

                MainViewModel.Http = new HttpClient(new StandInInternet("a.png"));
                Assert.False(await viewModel.DownloadExampleDataAsync(Data, All, Path.Combine(_folder, "broken")));
                Assert.Contains(viewModel.Output, o => o.IsError && o.Text.StartsWith("Test scans couldn't be downloaded:"));
            });
        }

        [Fact]
        public void The_radar_example_reads_its_scans_from_where_they_were_downloaded()
        {
            Ui.Run(() =>
            {
                string scans = Path.Combine(_folder, "scans");
                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.BoreasRadar.Name] = scans;
                settings.Save();

                Example radar = Examples.All.Single(e => e.Name == "Radar clutter map");
                Assert.Same(Examples.BoreasRadar, radar.Data);
                // Nothing downloaded yet: the short set's folder, where it would go.
                Assert.Equal(Path.Combine(scans, "short"), radar.Build().Nodes.OfType<FilesNode>().Single().Folder);

                // The short set is part of the full one: 12 of its 213 scans, including scans 3 and 25.
                (ExampleDataSet shortSet, ExampleDataSet fullSet) = (Examples.BoreasRadar.Sets[0], Examples.BoreasRadar.Sets[1]);
                Assert.Equal((12, 213), (shortSet.Files.Count, fullSet.Files.Count));
                Assert.Subset(fullSet.Files.ToHashSet(), shortSet.Files.ToHashSet());
                Assert.Equal([fullSet.Files[2], fullSet.Files[24]], [shortSet.Files[1], shortSet.Files[3]]);
                Assert.Equal(fullSet.Files.Order(), fullSet.Files);
            });
        }

        [Fact]
        public void Real_julia_notes_the_first_bearing_alarm_in_the_real_snapshots()
        {
            Ui.Run(async () =>
            {
                // Runs only where the snapshots were downloaded (by opening the example).
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.ImsBearings.Folder;
                string? snapshots = Examples.ImsBearings.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (snapshots is null || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.ImsBearings.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Bearing wear").Build();
                design.Nodes.OfType<SaveFile>().Single().Path = Path.Combine(_folder, "bearing-wear.csv");
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));

                // Bearing 1, 2.5 days before its outer race failed; the stopped rig's last snapshot has no row.
                bool full = Path.GetFileName(snapshots) == "full";
                Report alarm = reports.Last(r => r.Id == design.Nodes.OfType<DisplayNode>().Single().ID);
                Assert.Equal($"2004.02.16.{(full ? "05.52" : "10.32")}.39: bearing 1, 11% stronger than when new", alarm.Text);
                Assert.Equal(full ? 982 : 123, File.ReadAllLines(Path.Combine(_folder, "bearing-wear.csv")).Length);
            });
        }

        [Fact]
        public void Real_julia_finds_the_ridgecrest_quakes_in_the_real_seismograms()
        {
            Ui.Run(async () =>
            {
                // Runs only where the seismograms were downloaded (by opening the example).
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.RidgecrestSeismograms.Folder;
                string? minutes = Examples.RidgecrestSeismograms.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (minutes is null || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.RidgecrestSeismograms.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Earthquake detector").Build();
                design.Nodes.OfType<SaveFile>().Single().Path = Path.Combine(_folder, "quakes.csv");
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));

                // The M4.0 foreshock, an M2.5, an M1.5 100 km away, a 4 times the catalogue hasn't, the M6.4; then aftershocks.
                string[] quakes = File.ReadAllLines(Path.Combine(_folder, "quakes.csv"));
                string[] expected = ["17:03:37 UTC, 247×", "17:10:32 UTC, 6×", "17:27:34 UTC, 4×", "17:32:49 UTC, 4×", "17:34:43 UTC, 25×"];
                expected = Path.GetFileName(minutes) == "full" ? [.. expected, "18:02:41 UTC, 4×", "18:06:58 UTC, 6×", "18:19:57 UTC, 7×", "18:27:16 UTC, 10×"] : expected;
                Assert.Equal(expected.Length, quakes.Length);
                Assert.All(expected.Zip(quakes), pair => Assert.Contains(pair.First, pair.Second));
            });
        }

        [Fact]
        public void Real_julia_scores_what_moves_in_the_real_highway_frames()
        {
            Ui.Run(async () =>
            {
                // Runs only where the frames were downloaded (by opening the example) and PNG files can be read.
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.CdnetHighway.Folder;
                string? frames = Examples.CdnetHighway.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (frames is null
                    || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia
                    || (await new JuliaPackages(julia.Path).FindMissingAsync(["PNGFiles", "ColorTypes"], _folder)).Count > 0)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.CdnetHighway.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Motion in video").Build();
                design.Nodes.OfType<SaveFile>().Single().Path = Path.Combine(_folder, "masks", "mask.png");
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));
                bool full = Path.GetFileName(frames) == "full";
                Report score = reports.Last(r => r.Id == design.Nodes.OfType<DisplayNode>().Single().ID);
                Assert.Equal(full ? "(precision = 0.873, recall = 0.808, F = 0.839)" : "(precision = 0.835, recall = 0.812, F = 0.823)", score.Text);
                Assert.Equal(full ? 1700 : 800, Directory.GetFiles(Path.Combine(_folder, "masks")).Length);
            });
        }

        [Fact]
        public void Real_julia_confirms_only_the_quakes_two_stations_hear()
        {
            Ui.Run(async () =>
            {
                // Runs only where the network's minutes were downloaded (by opening the example).
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.RidgecrestNetwork.Folder;
                string? minutes = Examples.RidgecrestNetwork.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (minutes is null || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.RidgecrestNetwork.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Earthquake network").Build();
                design.Nodes.OfType<SaveFile>().Single().Path = Path.Combine(_folder, "quakes.csv");
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));

                // The M4.0 foreshock, an M2.5, an M2.1, an M1.5 and the M6.4, as the USGS catalogue lists them; the 3 hours
                // add 13 aftershocks, and nothing in the quiet hour before.
                string[] quakes = File.ReadAllLines(Path.Combine(_folder, "quakes.csv"));
                string[] expected =
                [
                    "17:03:21 UTC, heard at PFO, GSC, TIN", "17:09:46 UTC, heard at GSC, ISA", "17:12:38 UTC, heard at GSC, ISA",
                    "17:27:27 UTC, heard at PFO, TIN", "17:33:55 UTC, heard at PFO, GSC, ISA, TIN",
                ];
                Assert.Equal(Path.GetFileName(minutes) == "full" ? 18 : 5, quakes.Length);
                Assert.All(expected.Zip(quakes), pair => Assert.Contains(pair.First, pair.Second));
            });
        }

        [Fact]
        public void Real_julia_works_through_the_bearing_week_in_parallel_and_finds_when_bearing_1_wore_for_good()
        {
            Ui.Run(async () =>
            {
                // Runs only where the snapshots were downloaded (by opening a bearing example).
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.ImsBearings.Folder;
                string? snapshots = Examples.ImsBearings.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (snapshots is null || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.ImsBearings.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Bearing week in parallel").Build();
                Assert.True(design.Nodes.OfType<MapNode>().Single().InParallel);
                design.Nodes.OfType<SaveFile>().Single().Path = Path.Combine(_folder, "bearing-features.csv");
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));

                // A row a snapshot; bearing 1 worn for good from early on 16 February, the others only at the end.
                bool full = Path.GetFileName(snapshots) == "full";
                Assert.Equal(full ? 984 : 124, File.ReadAllLines(Path.Combine(_folder, "bearing-features.csv")).Length);
                string onset = reports.Last(r => r.Id == design.Nodes.OfType<DisplayNode>().Single().ID).Text;
                Assert.Contains(full ? "bearing 1: worn from 2004.02.16.05.52.39, 434 snapshots before the end" : "bearing 1: worn from 2004.02.16", onset);
                Assert.Contains("bearing 2: worn from 2004.02.18", onset);
            });
        }

        [Fact]
        public void Real_julia_finds_the_interference_in_the_real_scans()
        {
            Ui.Run(async () =>
            {
                // Runs only where the scans were downloaded (by opening the example) and PNG files can be read.
                AppSettings.SettingsPath = _originalSettingsPath;
                string folder = Examples.BoreasRadar.Folder;
                string? scans = Examples.BoreasRadar.DownloadedFolder;
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (scans is null
                    || await JuliaLocator.FindAsync(null) is not JuliaInstallation julia
                    || (await new JuliaPackages(julia.Path).FindMissingAsync(["PNGFiles", "ColorTypes"], _folder)).Count > 0)
                {
                    return;
                }

                AppSettings settings = AppSettings.Load();
                settings.DataFolders[Examples.BoreasRadar.Name] = folder;
                settings.Save();
                MainViewModel.DesignDocument design = Examples.All.Single(e => e.Name == "Radar clutter map").Build();
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, _folder);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                RunResult result = await session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));
                // The scans with another radar's interference, listed as the scans go by: the last list. The clutter
                // map builds up from the first scan: 25 and 144 of the full set; 2 and 4 of the short one, scans 3 and 25 of the stop.
                Report interference = reports.Last(r => script.Variables[r.Id] == "noted_out");
                Assert.Equal(Path.GetFileName(scans) == "full" ? "2-element Vector{Int64}:\n  25\n 144" : "2-element Vector{Int64}:\n 2\n 4", interference.Text);
                Assert.Equal("Matrix{Float64}", reports.Last(r => r.Id == design.Nodes.OfType<ArrayBuilder>().First().ID).Type);
            });
        }
    }
}
