using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Work in parallel: Julia runs with a thread a core, a Map can share its values out among them, and CSV and TSV
    /// files are read with Parsers, which (unlike readdlm on Windows) stays fast on several threads at once.
    /// </summary>
    public sealed class ParallelTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public ParallelTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void A_map_in_parallel_shares_its_values_out_among_the_threads_in_a_run_and_in_plain_julia()
        {
            Ui.Run(() =>
            {
                File.WriteAllText(Path.Combine(_folder, "a.csv"), "1,2\n");
                MainViewModel.LoadStartingDesign();
                FilesNode files = new() { Folder = _folder };
                Function total = new() { Name = "total", ReturnType = "Float64", Body = "sum(A)" };
                total.Parameters.Add(new Parameter("A", "Matrix{Float64}"));
                MapNode map = new();
                MainViewModel.Nodes.AddRange([files, total, map]);
                Assert.Null(MainViewModel.Connect(files, map, map.Items));
                Assert.Null(MainViewModel.Connect(total, map, map.Apply));

                Assert.Equal("map", map.Label);
                map.InParallel = true;
                Assert.Equal("map ∥", map.Label);

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);
                Assert.Contains("global mapped1 = _jsynth_map(total, files1; parallel = true)", run.Text);
                Assert.Contains(FileFormats.Prelude.TrimEnd(), run.Text);

                JuliaScript plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false);
                Assert.Contains(MapNode.ParallelPrelude.TrimEnd(), plain.Text);
                Assert.Contains("mapped1 = _jsynth_tmap(total, files1)", plain.Text);
                Assert.Contains(FileFormats.Prelude.TrimEnd(), plain.Text);
            });
        }

        [Fact]
        public void Real_julia_maps_in_parallel_on_every_core_keeping_the_order_and_saying_which_value_failed()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                string script = $$"""
                    {{FilesNode.Prelude}}
                    {{MapNode.Prelude}}
                    {{MapNode.ParallelPrelude}}
                    println("threads ", Threads.nthreads())
                    slow(x) = (s = 0.0; for i in 1:2_000_000; s += sin(i + x); end; (x, Threads.threadid()))
                    done = _jsynth_map(slow, 1:48; parallel = true)
                    println("in order ", first.(done) == 1:48, ", on ", length(unique(last.(done))), " threads")
                    println(_jsynth_tmap(x -> 2x, 1:5))
                    try
                        _jsynth_map(x -> x == 3 ? error("bad") : x, 1:5; parallel = true)
                    catch e
                        println(sprint(showerror, e))
                    end
                    """;
                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<OutputLine> output = new();
                RunResult result = await session.RunAsync(script, _folder, output.Enqueue);
                string[] lines = [.. output.Select(l => l.Text)];
                Assert.True(result.Succeeded, string.Join("\n", lines));

                // Started with a thread a core, and the work shared out among them.
                Assert.Contains($"threads {Environment.ProcessorCount}", lines);
                Assert.Contains(lines, l => l.StartsWith("in order true, on ", StringComparison.Ordinal) && (Environment.ProcessorCount == 1 || !l.EndsWith(" 1 threads", StringComparison.Ordinal)));
                Assert.Contains("[2, 4, 6, 8, 10]", lines);
                Assert.Contains("value 3: bad", lines);
            });
        }

        [Fact]
        public void Real_julia_reads_numbers_past_comments_headers_and_windows_line_ends_and_says_what_is_wrong()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                File.WriteAllText(Path.Combine(_folder, "good.tsv"), "# a sensor\r\nx\ty\r\n1\t2  # first\r\n\r\n3\t4\r\n");
                File.WriteAllText(Path.Combine(_folder, "ragged.csv"), "1,2\n3\n");
                File.WriteAllText(Path.Combine(_folder, "words.csv"), "1,2\n3,four\n");
                File.WriteAllText(Path.Combine(_folder, "empty.csv"), string.Empty);
                string Read(string name, string format, string type, bool header, bool comments) =>
                    FileFormats.Read(format, ArrayFile.PathLiteral(Path.Combine(_folder, name)), JuliaTypes.Matrix, type, header, "A", comments);

                string script = $$"""
                    using Parsers
                    {{FileFormats.Prelude}}
                    println({{Read("good.tsv", FileFormats.Tsv, "Int64", true, true)}})
                    println(size({{Read("empty.csv", FileFormats.Csv, "Float64", false, false)}}))
                    for read in (() -> {{Read("ragged.csv", FileFormats.Csv, "Float64", false, false)}}, () -> {{Read("words.csv", FileFormats.Csv, "Int64", false, false)}})
                        try
                            read()
                        catch e
                            println(sprint(showerror, e))
                        end
                    end
                    """;
                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<OutputLine> output = new();
                RunResult result = await session.RunAsync(script, _folder, output.Enqueue);
                string[] lines = [.. output.Select(l => l.Text)];
                Assert.True(result.Succeeded, string.Join("\n", lines));
                Assert.Equal<string[]>(["[1 2; 3 4]", "(0, 0)", "ArgumentError: line 2 of ragged.csv has 1 value; the lines before it have 2", "ArgumentError: line 2 of words.csv: \"four\" can't be read as Int64"], lines);
            });
        }
    }
}
