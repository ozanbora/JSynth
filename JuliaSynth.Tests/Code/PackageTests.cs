using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests.Code
{
    /// <summary>
    /// Finding the Julia packages a design needs that aren't installed, and installing them. Julia is played
    /// by stand-ins; the last test asks the real one when it is installed. Nothing is ever really installed.
    /// </summary>
    public sealed class PackageTests : IDisposable
    {
        private const string Missing = "\u001eJSYNTH-MISSING\t";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public PackageTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void The_scripts_look_packages_up_and_add_them()
        {
            Assert.Equal(
                "for name in [\"MatrixMarket\", \"JLD2\"]\n    Base.find_package(name) === nothing && println(\"\\x1eJSYNTH-MISSING\\t\", name)\nend\n",
                JuliaPackages.CheckScript(["MatrixMarket", "JLD2"]));
            Assert.Equal("import Pkg\nPkg.add([\"MatrixMarket\"])\n", JuliaPackages.InstallScript(["MatrixMarket"]));
            _ = Assert.Throws<ArgumentException>(() => JuliaPackages.InstallScript(["Evil\"); rm(\"x"]));
        }

        [Fact]
        public async Task Missing_packages_are_found_in_the_order_asked()
        {
            string julia = StandInJulia.Create(_folder, [$"{Missing}JLD2", $"{Missing}MatrixMarket", "other output"]);

            IReadOnlyList<string> missing = await new JuliaPackages(julia).FindMissingAsync(["MatrixMarket", "SparseArrays", "JLD2"], _folder);

            Assert.Equal(["MatrixMarket", "JLD2"], missing);
            Assert.Empty(await new JuliaPackages(julia).FindMissingAsync([], _folder));
        }

        [Fact]
        public async Task A_failed_check_says_why()
        {
            string julia = StandInJulia.Create(_folder, ["!ERROR: something broke"], exitCode: 1);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new JuliaPackages(julia).FindMissingAsync(["JLD2"], _folder));
            Assert.Contains("something broke", error.Message);
        }

        [Fact]
        public async Task Installing_passes_the_package_manager_output_on()
        {
            string julia = StandInJulia.Create(_folder, ["!   Resolving package versions...", "!    Updating registry"]);
            List<OutputLine> output = [];

            RunResult result = await new JuliaPackages(julia).InstallAsync(["JLD2"], _folder, line => { lock (output) { output.Add(line); } });

            Assert.True(result.Succeeded);
            lock (output)
            {
                Assert.Contains(output, line => line.Text.Contains("Resolving package versions"));
            }
        }

        /// <summary>
        /// Runs the design with a stand-in Julia that says MatrixMarket is missing, answering the question
        /// about installing it with <paramref name="choice"/>; returns the output panel's lines.
        /// </summary>
        private List<string> RunNeedingMatrixMarket(PackageChoice choice, List<(IReadOnlyList<string> Packages, bool OfferRun)> asked)
        {
            List<string> output = [];
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel.Nodes.Add(new ArrayFile { Path = "a.mtx" });
                MainViewModel viewModel = new()
                {
                    AskToInstall = (packages, offerRun) =>
                    {
                        asked.Add((packages, offerRun));
                        return Task.FromResult(choice);
                    },
                };

                string julia = StandInJulia.Create(_folder, ["julia version 1.11.2", $"{Missing}MatrixMarket"]);
                Assert.True(await viewModel.ChooseJuliaAsync(julia));
                await viewModel.RunDesignAsync();
                output = viewModel.Output.Select(o => o.Text).ToList();
            });
            return output;
        }

        [Fact]
        public void A_run_offers_to_install_missing_packages_and_does_so()
        {
            List<(IReadOnlyList<string> Packages, bool OfferRun)> asked = [];

            List<string> output = RunNeedingMatrixMarket(PackageChoice.Install, asked);

            (IReadOnlyList<string> packages, bool offerRun) = Assert.Single(asked);
            Assert.Equal(["MatrixMarket"], packages);
            Assert.True(offerRun);
            Assert.Contains("Checking packages: MatrixMarket, SparseArrays…", output);
            Assert.Contains("Not installed: MatrixMarket.", output);
            Assert.Contains(output, line => line.StartsWith("Installed MatrixMarket in "));
            Assert.Contains(output, line => line.StartsWith("Running with Julia 1.11.2"));
        }

        [Fact]
        public void A_run_can_go_ahead_without_the_missing_packages()
        {
            List<string> output = RunNeedingMatrixMarket(PackageChoice.RunAnyway, []);

            Assert.Contains("Running without MatrixMarket: the nodes needing it are skipped.", output);
            Assert.DoesNotContain(output, line => line.StartsWith("Installing"));
            Assert.Contains(output, line => line.StartsWith("Running with Julia 1.11.2"));
        }

        [Fact]
        public void Cancelling_stops_before_running_and_says_how_to_install_later()
        {
            List<string> output = RunNeedingMatrixMarket(PackageChoice.Cancel, []);

            Assert.Contains("Not installing. To install it yourself, run in Julia: import Pkg; Pkg.add([\"MatrixMarket\"])", output);
            Assert.DoesNotContain(output, line => line.StartsWith("Running with"));
        }

        [Fact]
        public void The_generator_lists_the_packages_a_design_needs()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Assert.Empty(ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Packages);

                MainViewModel.Nodes.AddRange([new ArrayFile { Path = "a.jld2" }, new ArrayBuilder { Kind = "identity" }]);
                Assert.Equal(["JLD2", "LinearAlgebra", "SparseArrays"], ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Packages);
            });
        }

        [Fact]
        public async Task Real_julia_finds_standard_libraries_but_not_unknown_packages()
        {
            // Nothing to check against without Julia.
            if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
            {
                return;
            }

            IReadOnlyList<string> missing = await new JuliaPackages(julia.Path).FindMissingAsync(["SparseArrays", "LinearAlgebra", "NoSuchJuliaSynthPackage"], _folder);

            Assert.Equal(["NoSuchJuliaSynthPackage"], missing);
        }

        [Fact]
        public void Opening_a_design_says_which_packages_it_needs_before_it_runs()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Assert.True(await viewModel.ChooseJuliaAsync(StandInJulia.Create(_folder, ["julia version 1.11.2", $"{Missing}MatrixMarket"])));
                Assert.False(viewModel.IsPackageBannerVisible);

                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Network spread"));
                _ = await Wait.Until(() => viewModel.IsPackageBannerVisible, 15);

                Assert.Equal(["MatrixMarket"], viewModel.MissingPackages);
                Assert.Equal("This design needs MatrixMarket, which isn't installed. The nodes needing it are skipped until it is.", viewModel.PackageBannerText);
                Assert.True(viewModel.CanOfferInstall);

                ((System.Windows.Input.ICommand)viewModel.DismissPackages).Execute(null);
                Assert.False(viewModel.IsPackageBannerVisible);

                // Another design that needs nothing missing: no banner.
                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Loan payment"));
                await Task.Delay(500);
                Assert.False(viewModel.IsPackageBannerVisible);
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Installing_from_the_banner_shows_it_going_and_ends_it()
        {
            Ui.Run(async () =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Assert.True(await viewModel.ChooseJuliaAsync(StandInJulia.Create(_folder, ["julia version 1.11.2", $"{Missing}MatrixMarket", "!   Resolving package versions..."])));
                viewModel.OpenExample(Examples.All.Single(e => e.Name == "Network spread"));
                _ = await Wait.Until(() => viewModel.IsPackageBannerVisible, 15);

                List<string> seen = [];
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.PackageBannerText) && viewModel.IsInstallingPackages)
                    {
                        seen.Add(viewModel.PackageBannerText);
                    }
                };

                await viewModel.InstallDesignPackagesAsync();

                Assert.Contains(seen, text => text.StartsWith("Installing MatrixMarket with Julia's package manager."));
                Assert.False(viewModel.IsInstallingPackages);
                Assert.False(viewModel.IsPackageBannerVisible);
                Assert.Contains(viewModel.Output, o => o.Text.Contains("Resolving package versions"));
                Assert.Contains(viewModel.Output, o => o.Text.StartsWith("Installed MatrixMarket in "));
                viewModel.EndJulia();
            });
        }

        [Fact]
        public void Real_julia_names_a_package_a_function_loads_that_is_not_installed()
        {
            Ui.Run(async () =>
            {
                // Nothing to check against without Julia.
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                Assert.True(await viewModel.ChooseJuliaAsync(julia.Path));
                string path = Path.Combine(_folder, "needs.jsynth");
                Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
                f.Body = "using NoSuchJuliaSynthPackage\nx ^ z";
                viewModel.SaveDesign(path);

                viewModel.OpenDesign(path);
                _ = await Wait.Until(() => viewModel.IsPackageBannerVisible, 15);

                Assert.Equal(["NoSuchJuliaSynthPackage"], viewModel.MissingPackages);
                viewModel.EndJulia();
            });
        }
    }
}
