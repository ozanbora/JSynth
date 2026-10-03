using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests
{
    public class ExampleTests
    {
        public static IEnumerable<object[]> Names => Examples.All.Select(e => new object[] { e.Name });

        private static Example Named(string name) => Examples.All.Single(e => e.Name == name);

        [Theory]
        [MemberData(nameof(Names))]
        public void An_example_is_complete_and_tidy(string name)
        {
            MainViewModel.DesignDocument design = Named(name).Build();

            // Everything computable: nothing unwired or invalid, so the plain script skips nothing.
            string script = ScriptGenerator.Generate(design.Nodes, design.Connections, instrumented: false).Text;
            // (A skipped node is a "# x1: why" comment; other comments, like how to plot in Julia, are fine.)
            Assert.DoesNotMatch(@"\n# \w+: ", script.Split("# Values")[1]);

            // No node on top of another.
            foreach (Node a in design.Nodes)
            {
                foreach (Node b in design.Nodes.Where(n => n != a))
                {
                    (double top, double left, double bottom, double right) = Cells(a);
                    (double otherTop, double otherLeft, double otherBottom, double otherRight) = Cells(b);
                    bool overlap = top < otherBottom && otherTop < bottom && left < otherRight && otherLeft < right;
                    Assert.False(overlap, $"{Label(a)} overlaps {Label(b)}");
                }
            }
        }

        /// <summary>
        /// Where a node sits, in cells: centred on its cell, 9×6 cells (a Display 13×9, a Plot 16×13); nodes
        /// with a row per input grow down a cell per input past the second.
        /// </summary>
        private static (double Top, double Left, double Bottom, double Right) Cells(Node node)
        {
            (double width, double height) = node switch
            {
                DisplayNode => (13, 9),
                PlotNode => (16, 13),
                _ => (9, 6),
            };
            double top = node.Row + 0.5 - (height / 2);
            if (node is not (DisplayNode or PlotNode) && node.Inputs.Count > 2)
            {
                height += node.Inputs.Count - 2;
            }

            return (top, node.Column + 0.5 - (width / 2), top + height, node.Column + 0.5 + (width / 2));
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

            string folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
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

                JuliaRunner runner = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                runner.Reported += reports.Enqueue;

                RunResult result = await runner.RunAsync(script.Text, folder);

                Assert.True(result.Succeeded);
                Assert.All(reports, r => Assert.True(r.Status == ReportStatus.Ok, $"{script.Variables[r.Id]}: {r.Text}"));
                // Every node reports, but the functions a Map applies: they have no value of their own.
                int applied = design.Connections
                    .Where(c => design.Nodes.Single(n => n.ID == c.TargetID).Inputs.Single(p => p.ID == c.ParameterID).IsFunctionPort)
                    .Select(c => c.SourceID)
                    .Distinct()
                    .Count();
                Assert.Equal(design.Nodes.Count - applied, reports.Count);
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
