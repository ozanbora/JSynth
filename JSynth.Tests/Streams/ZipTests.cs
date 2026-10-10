using JSynth.Model;
using JSynth.Runtime;
using JSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace JSynth.Tests.Streams
{
    /// <summary>
    /// Several streams at once: folders wired into a Zip node go through their files in step, matched by name (the
    /// same minute of several stations), the zip giving a vector of them; a name not in every folder is passed over,
    /// and a watched file waits for its twins.
    /// </summary>
    public sealed class ZipTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public ZipTests()
        {
            _ = Directory.CreateDirectory(Path.Combine(_folder, "a"));
            _ = Directory.CreateDirectory(Path.Combine(_folder, "b"));
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private string A => Path.Combine(_folder, "a");

        private string B => Path.Combine(_folder, "b");

        /// <summary>
        /// a has 1, 2 and 3, b only 1 and 3 (10 and 30): 2 isn't in step.
        /// </summary>
        private void Write()
        {
            foreach ((string name, int value) in new[] { ("1.csv", 1), ("2.csv", 2), ("3.csv", 3) })
            {
                File.WriteAllText(Path.Combine(A, name), $"{value}\n");
            }

            File.WriteAllText(Path.Combine(B, "1.csv"), "10\n");
            File.WriteAllText(Path.Combine(B, "3.csv"), "30\n");
        }

        /// <summary>
        /// <paramref name="first"/> and <paramref name="second"/> → Zip → sum(F) → Display.
        /// </summary>
        private static (ZipNode Zip, Function Sum) Design(FilesNode first, FilesNode second)
        {
            new MainViewModel().NewDesign();
            ZipNode zip = new();
            Function sum = new() { Name = "both", ReturnType = "Float64", Body = "F[1][1] + F[2][1]" };
            sum.Parameters.Add(new Parameter("F", JuliaTypes.Vector));
            MainViewModel.Nodes.AddRange([first, second, zip, sum]);
            Assert.Null(MainViewModel.Connect(first, zip, zip.Streams[0]));
            Assert.Null(MainViewModel.Connect(second, zip, zip.Streams[1]));
            Assert.Null(MainViewModel.Connect(zip, sum, sum.Parameters[0]));
            return (zip, sum);
        }

        [Fact]
        public void Wiring_a_zips_last_port_adds_one_and_a_folder_wired_in_goes_one_file_at_a_time()
        {
            Ui.Run(Wire);
        }

        private void Wire()
        {
            Write();
            new MainViewModel().NewDesign();
            ZipNode zip = new();
            FilesNode a = new() { Folder = A };
            FilesNode b = new() { Folder = B };
            MainViewModel.Nodes.AddRange([a, b, zip]);
            Assert.Equal(["stream 1", "stream 2"], zip.Streams.Select(p => p.Name));
            Assert.Equal(JuliaTypes.Vector, zip.OutputType);

            // One folder: nothing to go in step with yet.
            Assert.Null(MainViewModel.Connect(a, zip, zip.Streams[0]));
            Assert.True(a.EachFile);
            Assert.Equal(2, zip.Streams.Count);
            string script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
            Assert.Contains("# zipped1: Wire in two folders or more, to go through their files in step.", script);

            // Two: the loop goes through a's files that b has too, b's of the same name with each.
            Assert.Null(MainViewModel.Connect(b, zip, zip.Streams[1]));
            Assert.Equal(["stream 1", "stream 2", "stream 3"], zip.Streams.Select(p => p.Name));
            script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
            Assert.Contains($"for _path in _jsynth_in_step(JSynthFiles(raw\"{A}\", r\"^.*\\.csv$\"i, path -> _jsynth_read_numbers(path, ',', Float64)), [raw\"{B}\"]).paths", script);
            Assert.Contains($"global frame2 = (path -> _jsynth_read_numbers(path, ',', Float64))(joinpath(raw\"{B}\", basename(_path)))", script);
            Assert.Contains("global zipped1 = collect((frame1, frame2,))", script);

            // Only folders go in step.
            Float x = new();
            MainViewModel.Nodes.Add(x);
            Assert.Null(MainViewModel.Connect(x, zip, zip.Streams[2]));
            script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
            Assert.Contains("# zipped1: Only folders go in step: wire in Files nodes or Watch folders.", script);
        }

        [Fact]
        public void A_zip_keeps_its_ports_and_wires_when_saved_and_opened()
        {
            string settingsBefore = AppSettings.SettingsPath;
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    Write();
                    (ZipNode zip, _) = Design(new FilesNode { Folder = A }, new FilesNode { Folder = B });
                    MainViewModel viewModel = new();
                    viewModel.SaveDesign(Path.Combine(_folder, "zipped.jsynth"));

                    viewModel.NewDesign();
                    viewModel.OpenDesign(Path.Combine(_folder, "zipped.jsynth"));
                    ZipNode reopened = MainViewModel.Nodes.OfType<ZipNode>().Single();
                    Assert.Equal(zip.Streams.Select(p => p.ID), reopened.Streams.Select(p => p.ID));
                    Assert.Equal(2, MainViewModel.Connections.Count(c => c.TargetID == reopened.ID));
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
            }
        }

        [Fact]
        public void Real_julia_goes_through_the_files_in_step_plain_and_in_a_run()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                Write();
                FilesNode a = new() { Folder = A };
                FilesNode b = new() { Folder = B };
                (ZipNode zip, Function sum) = Design(a, b);
                using JuliaSession session = new(julia.Path);

                // Plain Julia: 1 + 10, then 3 + 30; 2 has no twin.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text + "\nprintln(both_out)\n";
                ConcurrentQueue<OutputLine> output = new();
                RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                Assert.True(result.Succeeded, string.Join("\n", output.Select(l => l.Text)));
                Assert.Contains(output, l => l.Text == "33.0");

                // Each folder says which file it is at.
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder);
                ConcurrentQueue<Report> reports = new();
                Assert.True((await session.RunAsync(script.Text, _folder, reported: Finished.Into(reports))).Succeeded);
                Assert.Equal("33.0", reports.Last(r => r.Id == sum.ID).Text);
                Assert.Equal("file 2 of 2: 3.csv", reports.Last(r => r.Id == a.ID).Text);
                Assert.Equal("file 2 of 2: 3.csv", reports.Last(r => r.Id == b.ID).Text);
                Assert.Equal("Vector{Matrix{Float64}}", reports.Last(r => r.Id == zip.ID).Type);
            });
        }

        [Fact]
        public void Real_julia_waits_for_a_watched_files_twins()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                // Both folders watched: the loop goes through the first one's files, the second's of the same names going with them.
                WatchNode watch = new() { Folder = A, ExistingFirst = false };
                (_, Function sum) = Design(watch, new WatchNode { Folder = B, ExistingFirst = false });
                string stop = Path.Combine(_folder, "stop");
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder, stop);
                Assert.Same(watch, script.Watched);
                Assert.Contains($"also = [raw\"{B}\"]", script.Text);
                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                Task<RunResult> running = session.RunAsync(script.Text, _folder, reported: Finished.Into(reports));

                // a's file alone: nothing yet, however long; its twin written: worked out.
                _ = await Wait.Until(() => reports.Any(r => r.Id == watch.ID && r.Text == WatchNode.WatchingText), 60);
                File.WriteAllText(Path.Combine(A, "1.csv"), "1\n");
                _ = await Wait.Until(() => reports.Any(r => r.Id == sum.ID), 1.5);
                Assert.DoesNotContain(reports, r => r.Id == sum.ID);
                File.WriteAllText(Path.Combine(B, "1.csv"), "10\n");
                _ = await Wait.Until(() => reports.Any(r => r.Id == sum.ID), 30);
                Assert.Equal("11.0", reports.Last(r => r.Id == sum.ID).Text);

                File.WriteAllText(stop, string.Empty);
                Assert.True((await running.WaitAsync(TimeSpan.FromSeconds(30))).Succeeded);
            });
        }

        [Fact]
        public void A_geocsv_file_in_pieces_is_joined_into_one()
        {
            string pieces = "# dataset: GeoCSV 2.0\n# sample_count: 2\n# start_time: 17:03:00\nSample\n1\n-2\n"
                + "# dataset: GeoCSV 2.0\n# sample_count: 1\n# start_time: 17:03:02\nSample\n3\n";
            using MemoryStream from = new(Encoding.UTF8.GetBytes(pieces));
            using MemoryStream to = new();
            ExampleData.JoinGeoCsv(from, to);
            Assert.Equal("# dataset: GeoCSV 2.0\n# sample_count: 3\n# start_time: 17:03:00\nSample\n1\n-2\n3\n", Encoding.UTF8.GetString(to.ToArray()));
        }

        [Fact]
        public void The_network_minutes_are_in_a_folder_a_station_with_the_same_names()
        {
            (ExampleDataSet shortSet, ExampleDataSet fullSet) = (Examples.RidgecrestNetwork.Sets[0], Examples.RidgecrestNetwork.Sets[1]);
            Assert.Equal((240, 720), (shortSet.Files.Count, fullSet.Files.Count));
            Assert.Equal(["PFO", "GSC", "ISA", "TIN"], shortSet.Files.Select(f => f.Split('/')[0]).Distinct());
            Assert.Equal("PFO/2019-07-04T16-40.csv", shortSet.Files[0]);
            Assert.Equal(
                "https://service.earthscope.org/fdsnws/dataselect/1/query?net=CI&sta=GSC&loc=--&cha=BHZ&start=2019-07-04T17:03:00&end=2019-07-04T17:04:00&format=geocsv.slist",
                Examples.RidgecrestNetwork.FileUrl!("GSC/2019-07-04T17-03.csv"));
        }
    }
}
