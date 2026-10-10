using JSynth.Model;
using JSynth.Runtime;
using JSynth.Tests.Arrays;
using JSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JSynth.Tests.Files
{
    public class ExampleTests
    {
        public static IEnumerable<object[]> Names => Examples.All.Select(e => new object[] { e.Name });

        private static Example Named(string name) => Examples.All.Single(e => e.Name == name);

        /// <summary>
        /// For an example whose data is downloaded: tiny files of the same names (PNG images, or tables) in a temporary folder,
        /// remembered as where the data was downloaded to, in settings of their own, until disposed.
        /// </summary>
        private sealed class StandInData : IDisposable
        {
            private readonly string _settingsBefore = AppSettings.SettingsPath;
            private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

            public StandInData(Example example)
            {
                _ = Directory.CreateDirectory(_folder);
                AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
                if (example.Data is ExampleData data)
                {
                    string files = ExampleData.SetFolder(_folder, data.Sets[0]);
                    _ = Directory.CreateDirectory(files);
                    foreach (string file in data.Sets[0].Files)
                    {
                        _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(files, file))!);
                        if (file.EndsWith(".png", StringComparison.Ordinal))
                        {
                            PngTests.WriteGreyPng(Path.Combine(files, file), new int[1, 1], 8);
                        }
                        else
                        {
                            // A table of readings: a bearing snapshot, or a seismometer minute.
                            File.WriteAllText(Path.Combine(files, file), "0.1\t0.2\t-0.1\t0.3\n-0.2\t0.1\t0.2\t-0.3\n");
                        }
                    }

                    AppSettings settings = AppSettings.Load();
                    settings.DataFolders[data.Name] = _folder;
                    settings.Save();
                }
            }

            public void Dispose()
            {
                AppSettings.SettingsPath = _settingsBefore;
                Directory.Delete(_folder, recursive: true);
            }
        }

        // One Julia for all the examples, as in the app: each design runs after the others, without their names.
        private static readonly Dictionary<string, JuliaSession> Sessions = [];

        private static JuliaSession SessionFor(JuliaInstallation julia)
        {
            lock (Sessions)
            {
                return Sessions.TryGetValue(julia.Path, out JuliaSession? session) ? session : Sessions[julia.Path] = new JuliaSession(julia.Path);
            }
        }

        [Theory]
        [MemberData(nameof(Names))]
        public void An_example_is_complete_and_tidy(string name)
        {
            using StandInData data = new(Named(name));
            MainViewModel.DesignDocument design = Named(name).Build();

            // Everything computable: nothing unwired or invalid, so the plain script skips nothing.
            string script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: false).Text;
            // (A skipped node is a "# x1: why" comment; other comments, like how to plot in Julia, are fine.)
            Assert.DoesNotMatch(@"\n# \w+: ", script.Split("# Values")[1]);

            // No node on top of another; a frame only around the nodes it holds, whole, apart from the other frames.
            foreach (Node a in design.Nodes)
            {
                foreach (Node b in design.Nodes.Where(n => n != a && !(a is FrameNode frame && frame.Holds(n))))
                {
                    if (b is FrameNode around && around.Holds(a))
                    {
                        (double top, double left, double bottom, double right) = Layout.Footprint(a);
                        Assert.True(top >= around.Row + FrameNode.TitleRows && left > around.Column && bottom < around.Row + around.Rows && right < around.Column + around.Columns,
                            $"{Label(a)} is not all in {around.Title}");
                        continue;
                    }

                    Assert.False(Overlap(a, b), $"{Label(a)} overlaps {Label(b)}");
                }
            }
        }

        private static bool Overlap(Node a, Node b)
        {
            (double top, double left, double bottom, double right) = Layout.Footprint(a);
            (double otherTop, double otherLeft, double otherBottom, double otherRight) = Layout.Footprint(b);
            return top < otherBottom && otherTop < bottom && left < otherRight && otherLeft < right;
        }

        private static string Label(Node node) => node is Function f ? f.Name : $"{node.GetType().Name} at {node.Row},{node.Column}";

        [Fact]
        public void Opening_an_example_makes_a_new_unsaved_design_and_explains_it()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();

                viewModel.OpenExample(Named("Loan payment"));

                Assert.Null(viewModel.FilePath);
                Assert.False(viewModel.IsDirty);
                Assert.False(MainViewModel.History.CanUndo);
                Assert.Contains(MainViewModel.Nodes.OfType<Function>(), f => f.Name == "payment");
                Assert.True(viewModel.IsOutputVisible);
                Assert.StartsWith("Example: Loan payment. Finance:", viewModel.Output.Last().Text);
            });
        }

        /// <summary>
        /// The value each named variable should have, within a tolerance.
        /// </summary>
        public static IEnumerable<object[]> Answers =>
        [
            ["Loan payment", "payment_out", 1266.713, 0.001],
            ["Loan payment", "interest_out", 206016.779, 0.001],
            ["Projectile", "distance_out", 40.775, 0.001],
            ["Projectile", "peak_out", 10.194, 0.001],
            ["Projectile", "airtime_out", 2.883, 0.001],
            ["Line fit", "predict_out", 82.171, 0.001],
            ["PageRank", "top_out", 3.0, 0.0],
            ["Heat in a rod", "hottest_out", 0.125, 0.0002],
            ["Monte Carlo π", "estimate_out", Math.PI, 0.05],
            ["Weather station week", "warmest_out", 3.0, 0.0],
            ["Network spread", "fastest_out", 1.0, 0.0],
            ["Savings growth", "final_out", 43219.424, 0.001],
        ];

        /// <summary>
        /// How many values a plot of the example draws.
        /// </summary>
        private static int PlottedValues(string name) => name switch
        {
            "Heat in a rod" => 50,
            "Weather station week" => 7,
            "Savings growth" => 31,
            _ => 100_000,
        };

        [Theory]
        [MemberData(nameof(Answers))]
        public async Task An_example_gives_the_right_answer_with_real_julia(string name, string variable, double expected, double tolerance)
        {
            // Nothing to check against without Julia.
            if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
            {
                return;
            }

            string folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(folder);
            try
            {
                MainViewModel.DesignDocument design = Named(name).Build();
                JuliaScript script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: true, folder);

                // A package that isn't installed (MatrixMarket, for .mtx files) is the app's to offer; nothing to check here.
                if ((await new JuliaPackages(julia.Path).FindMissingAsync(script.Packages, folder)).Count > 0)
                {
                    return;
                }

                ConcurrentQueue<Report> reports = new();

                RunResult result = await SessionFor(julia).RunAsync(script.Text, folder, reported: Finished.Into(reports));

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));
                // Every node reports, but the functions a Map applies: they have no value of their own.
                int applied = design.Connections
                    .Where(c => design.Nodes.Single(n => n.ID == c.TargetID).Inputs.Single(p => p.ID == c.ParameterID).IsFunctionPort)
                    .Select(c => c.SourceID)
                    .Distinct()
                    .Count();
                Assert.Equal(MainViewModel.AllNodes(design.Nodes).Count(n => n is not (FrameNode or GroupNode)) - applied, reports.Count);
                Report answer = reports.Single(r => script.Variables[r.Id] == variable);
                Assert.InRange(double.Parse(answer.Text, CultureInfo.InvariantCulture), expected - tolerance, expected + tolerance);

                // Plots wrote their numbers where the app reads them.
                foreach (PlotNode plot in design.Nodes.OfType<PlotNode>())
                {
                    PlotData data = PlotData.Read(reports.Single(r => r.Id == plot.ID).Text);
                    Assert.Equal(PlottedValues(name), data.Y.Length);
                }
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
