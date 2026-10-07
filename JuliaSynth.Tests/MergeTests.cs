using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Several streams merged into one: the files of the folders wired into a Merge node are gone through together, in
    /// name order (or as they are written, watched); the merge gives the file just come, each folder its latest file,
    /// and the nodes after a folder wait for its first.
    /// </summary>
    public sealed class MergeTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public MergeTests()
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
        /// a has 1 and 3, b 2 and 3 (20 and 30): merged, 1, 20, 3 then 30 (for the same name, a first).
        /// </summary>
        private void Write()
        {
            File.WriteAllText(Path.Combine(A, "1.csv"), "1\n");
            File.WriteAllText(Path.Combine(A, "3.csv"), "3\n");
            File.WriteAllText(Path.Combine(B, "2.csv"), "20\n");
            File.WriteAllText(Path.Combine(B, "3.csv"), "30\n");
        }

        private sealed record Merged(MergeNode Merge, Function Seen, Function Sums, Function Folder);

        /// <summary>
        /// a and b → Merge; seen: every file's value so far, through the merge; sums: a's latest plus b's latest, so far;
        /// and which: the folder of the file, from the merge's folder output.
        /// </summary>
        private static Merged Design(FilesNode a, FilesNode b)
        {
            new MainViewModel().NewDesign();
            MergeNode merge = new();
            Function both = Define("both", "Float64", "A[1] + B[1]", ("A", "Matrix{Float64}"), ("B", "Matrix{Float64}"));
            Function seen = Define("seen", "Vector{Float64}", "[S; m[1]]", ("S", "Vector{Float64}"), ("m", "Matrix{Float64}"));
            Function sums = Define("sums", "Vector{Float64}", "[S; s]", ("S", "Vector{Float64}"), ("s", "Float64"));
            ArrayBuilder seenSoFar = Empty();
            ArrayBuilder sumsSoFar = Empty();
            Function folder = Define("which", "String", "f", ("f", "String"));
            MainViewModel.Nodes.AddRange([a, b, merge, both, seen, sums, seenSoFar, sumsSoFar, folder]);
            Assert.Null(MainViewModel.Connect(merge, folder, folder.Parameters[0], MergeNode.FolderOutput));
            foreach ((Node source, Node target, Parameter port) in new (Node, Node, Parameter)[]
            {
                (a, merge, merge.Folders), (b, merge, merge.Folders),
                (seenSoFar, seen, seen.Parameters[0]), (merge, seen, seen.Parameters[1]), (seen, seenSoFar, seenSoFar.Next!),
                (a, both, both.Parameters[0]), (b, both, both.Parameters[1]),
                (sumsSoFar, sums, sums.Parameters[0]), (both, sums, sums.Parameters[1]), (sums, sumsSoFar, sumsSoFar.Next!),
            })
            {
                Assert.Null(MainViewModel.Connect(source, target, port));
            }

            return new Merged(merge, seen, sums, folder);
        }

        private static Function Define(string name, string returnType, string body, params (string Name, string Type)[] parameters)
        {
            Function function = new() { Name = name, ReturnType = returnType, Body = body };
            foreach ((string parameter, string type) in parameters)
            {
                function.Parameters.Add(new Parameter(parameter, type));
            }

            return function;
        }

        private static ArrayBuilder Empty()
        {
            ArrayBuilder empty = new() { Kind = "zeros", Shape = JuliaTypes.Vector, ElementType = "Float64" };
            foreach (Argument size in empty.Parameters.OfType<Argument>().Where(a => a.Key == "n"))
            {
                size.Value = "0";
            }

            return empty;
        }

        [Fact]
        public void A_merge_goes_through_every_folders_files_in_name_order_and_says_what_it_is_missing()
        {
            Ui.Run(() =>
            {
                Write();
                FilesNode a = new() { Folder = A, Row = 6 };
                FilesNode b = new() { Folder = B, Row = 18 };
                Merged design = Design(a, b);
                Assert.Equal(Function.AnyType, design.Merge.OutputType);

                // One port, any number of wires to it; wiring a folder again doesn't add another.
                MergeNode merge = design.Merge;
                Assert.Single(merge.Inputs);
                Assert.Equal(2, MainViewModel.Connections.Count(c => c.ParameterID == merge.Folders.ID));
                Assert.Null(MainViewModel.Connect(a, merge, merge.Folders));
                Assert.Equal(2, MainViewModel.Connections.Count(c => c.ParameterID == merge.Folders.ID));
                Assert.Equal(a.ID, MainViewModel.ConnectionTo(merge.Folders)!.SourceID);

                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains("for _path in _jsynth_merged([JuliaSynthFiles(", plain);
                Assert.Contains($"    _stream = _jsynth_stream(_path, [raw\"{A}\", raw\"{B}\"])", plain);
                Assert.Contains("    if _stream == 2\n        global frame2 = ", plain);
                Assert.Contains("    global merged1 = _stream == 1 ? frame1 : frame2", plain);

                // b's latest is needed: not before its first file.
                Assert.Contains("    if @isdefined(frame1) && @isdefined(frame2)\n        global both_out = both(frame1, frame2)", plain);
                Assert.Contains("    global merged1_folder = (\"a\", \"b\",)[_stream]", plain);
                Assert.Contains("    global which_out = which(merged1_folder)", plain);

                // The folders numbered top to bottom on the canvas: for the same name, the higher one's file first.
                (a.Row, b.Row) = (30, 6);
                plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains($"    _stream = _jsynth_stream(_path, [raw\"{B}\", raw\"{A}\"])", plain);

                // Only folders go together.
                Float x = new();
                MainViewModel.Nodes.Add(x);
                Assert.Null(MainViewModel.Connect(x, merge, merge.Folders));
                plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains("# merged1: Only folders go together: wire in Files nodes or Watch folders.", plain);
            });
        }

        [Fact]
        public void Helenes_files_are_cut_from_a_download_a_gauge_and_one_of_the_hurricane_database()
        {
            ExampleData data = Examples.HeleneSurge;
            Assert.Equal(454, data.Sets[0].Files.Count);
            Assert.Equal(13, data.Sets[0].Files.Select(data.FileUrl!).Distinct().Count());
            Assert.Contains("Cedar Key/2024-09-27T05-00.csv", data.Sets[0].Files);
            Assert.Contains("Helene/2024-09-27T03-10.csv", data.Sets[0].Files);

            string Convert(string name, string text)
            {
                using MemoryStream to = new();
                data.Convert!(name, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), to);
                return System.Text.Encoding.UTF8.GetString(to.ToArray());
            }

            // A gauge's hour: the readings of the hour its name ends, a missing one left out.
            string readings = "Date Time, Water Level, Sigma\n2024-09-27 03:54,3.1,0\n2024-09-27 04:00,3.867,0\n2024-09-27 04:06,,0\n2024-09-27 04:54,3.99,0\n2024-09-27 05:00,4.0,0\n";
            Assert.Equal("day,level\n27.16667,3.867\n27.20417,3.99\n", Convert("Cedar Key/2024-09-27T05-00.csv", readings));

            // The storm's position at the file's time, from its lines in the database.
            string hurdat = "AL082024,             GORDON,     3,\n20240927, 0310,  , HU, 10.0N,  50.0W, 30, 1010,\n"
                + "AL092024,             HELENE,     25,\n20240927, 0310, L, HU, 30.0N,  83.7W, 120,  939,  240\n";
            Assert.Equal("day,latitude,longitude,wind,pressure\n27.13194,30.0,-83.7,120,939\n", Convert("Helene/2024-09-27T03-10.csv", hurdat));
        }

        [Fact]
        public void A_merge_keeps_its_one_port_and_all_its_wires_when_saved_and_opened()
        {
            string settingsBefore = AppSettings.SettingsPath;
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    Write();
                    Merged design = Design(new FilesNode { Folder = A }, new FilesNode { Folder = B });
                    MainViewModel viewModel = new();
                    viewModel.SaveDesign(Path.Combine(_folder, "merged.jsynth"));

                    viewModel.NewDesign();
                    viewModel.OpenDesign(Path.Combine(_folder, "merged.jsynth"));
                    MergeNode reopened = MainViewModel.Nodes.OfType<MergeNode>().Single();
                    Assert.True(reopened.Folders.TakesMany);
                    Assert.Equal(design.Merge.Folders.ID, reopened.Folders.ID);
                    Assert.Equal(2, MainViewModel.Connections.Count(c => c.ParameterID == reopened.Folders.ID));
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
            }
        }

        [Fact]
        public void A_merges_folder_output_is_a_string_wired_on_its_own()
        {
            Ui.Run(() =>
            {
                Merged design = Design(new FilesNode { Folder = A }, new FilesNode { Folder = B });
                MergeNode merge = design.Merge;
                Assert.Equal("String", merge.OutputTypeOf(MergeNode.FolderOutput));

                // Its folder output is wired, not its value; and a string can't go where a matrix goes.
                Connection wire = MainViewModel.Connections.Single(c => c.TargetID == design.Folder.ID);
                Assert.Equal((merge.ID, MergeNode.FolderOutput), (wire.SourceID, wire.Output));
                Function both = MainViewModel.Nodes.OfType<Function>().Single(f => f.Name == "both");
                Assert.Equal("String can't be passed to A::Matrix{Float64}.", MainViewModel.Connect(merge, both, both.Parameters[0], MergeNode.FolderOutput));
            });
        }

        [Fact]
        public void Real_julia_merges_the_folders_with_each_ones_latest_plain_and_in_a_run()
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
                Merged design = Design(a, b);
                using JuliaSession session = new(julia.Path);

                // Every file in turn; a's latest plus b's, from b's first file on.
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text
                    + "\nprintln(seen_out)\nprintln(sums_out)\nprintln(which_out)\n";
                ConcurrentQueue<OutputLine> output = new();
                RunResult result = await session.RunAsync(plain, _folder, output.Enqueue);
                Assert.True(result.Succeeded, string.Join("\n", output.Select(l => l.Text)));
                Assert.Equal(["[1.0, 20.0, 3.0, 30.0]", "[21.0, 23.0, 33.0]", "b"], output.Select(l => l.Text));

                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder);
                ConcurrentQueue<Report> reports = new();
                Assert.True((await session.RunAsync(script.Text, _folder, reported: reports.Enqueue)).Succeeded);
                string[] Values(Node node) => [.. reports.Last(r => r.Id == node.ID).Text.Split('\n').Skip(1).Select(l => l.Trim())];
                Assert.Equal<string[]>(["1.0", "20.0", "3.0", "30.0"], Values(design.Seen));
                Assert.Equal<string[]>(["21.0", "23.0", "33.0"], Values(design.Sums));
                Assert.DoesNotContain(reports, r => r.Status == ReportStatus.Error);
                Assert.Equal("file 4 of 4: 3.csv", reports.Last(r => r.Id == b.ID).Text);
            });
        }

        [Fact]
        public void Real_julia_goes_through_watched_folders_files_as_they_come()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                WatchNode a = new() { Folder = A, ExistingFirst = false };
                WatchNode b = new() { Folder = B, ExistingFirst = false };
                Merged design = Design(a, b);
                string stop = Path.Combine(_folder, "stop");
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true, _folder, stop);
                Assert.Same(a, script.Watched);
                Assert.Contains($"more = [(raw\"{B}\", r\"^.*\\.csv$\"i)]", script.Text);
                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                Task<RunResult> running = session.RunAsync(script.Text, _folder, reported: reports.Enqueue);

                // b's file first, then a's: in the order they came, whatever their names.
                _ = await Wait.Until(() => reports.Any(r => r.Id == a.ID && r.Text == WatchNode.WatchingText), 60);
                File.WriteAllText(Path.Combine(B, "9.csv"), "90\n");
                _ = await Wait.Until(() => reports.Any(r => r.Id == design.Seen.ID), 30);
                File.WriteAllText(Path.Combine(A, "1.csv"), "1\n");
                _ = await Wait.Until(() => reports.Any(r => r.Id == design.Sums.ID), 30);
                Assert.Contains("91.0", reports.Last(r => r.Id == design.Sums.ID).Text);
                Assert.Contains("90.0", reports.Last(r => r.Id == design.Seen.ID).Text);
                Assert.Equal("a", reports.Last(r => r.Id == design.Folder.ID).Text);

                File.WriteAllText(stop, string.Empty);
                Assert.True((await running.WaitAsync(TimeSpan.FromSeconds(30))).Succeeded);
            });
        }
    }
}
