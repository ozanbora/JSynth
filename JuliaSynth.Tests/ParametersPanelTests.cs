using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The parameters panel: inputs set with sliders and boxes, the design running again a moment after each change.
    /// </summary>
    public sealed class ParametersPanelTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));
        private readonly string _originalSettingsPath = AppSettings.SettingsPath;

        public ParametersPanelTests()
        {
            _ = Directory.CreateDirectory(_folder);
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
        }

        public void Dispose()
        {
            AppSettings.SettingsPath = _originalSettingsPath;
            Directory.Delete(_folder, recursive: true);
        }

        private static Example Loan => Examples.All.Single(e => e.Name == "Loan payment");

        /// <summary>
        /// The Loan payment example with nothing in the panel, and the panel closed.
        /// </summary>
        private static void OpenBareLoan(MainViewModel viewModel)
        {
            viewModel.OpenExample(Loan);
            foreach (Node node in MainViewModel.Nodes)
            {
                (node.InPanel, node.PanelName, node.PanelFrom, node.PanelTo) = (false, string.Empty, null, null);
            }

            viewModel.IsParametersOpen = false;
        }

        [Fact]
        public void The_loan_example_opens_with_its_amount_rate_and_years_in_the_panel()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.OpenExample(Loan);
                Assert.True(viewModel.IsParametersOpen);
                Assert.Equal(["loan", "rate", "years"], viewModel.Parameters.Select(p => p.Name));
                Assert.Equal([(10_000.0, 1_000_000.0), (0, 0.15), (1, 40)], viewModel.Parameters.Select(p => (p.From, p.To)));
            });
        }

        [Fact]
        public void A_slider_starts_from_zero_to_a_round_number_past_twice_the_value()
        {
            Assert.Equal((0, 500000), PanelParameter.RangeAround(250000));
            Assert.Equal((0, 0.1), PanelParameter.RangeAround(0.045));
            Assert.Equal((0, 100), PanelParameter.RangeAround(30));
            Assert.Equal((0, 10), PanelParameter.RangeAround(0));
            Assert.Equal((-10, 10), PanelParameter.RangeAround(-3));
        }

        [Fact]
        public void Inputs_put_in_the_panel_are_named_after_where_they_go_and_set_from_it()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                OpenBareLoan(viewModel);
                Float loan = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).First();
                Integer years = MainViewModel.Nodes.OfType<Integer>().Single();
                loan.Result = new Report(loan.ID, ReportStatus.Ok, "Float64", "250000.0");

                Assert.False(viewModel.IsParametersOpen);
                loan.InPanel = years.InPanel = true;
                Assert.True(viewModel.IsParametersOpen);
                Assert.NotNull(loan.Result);

                PanelParameter p = viewModel.Parameters[0];
                PanelParameter y = viewModel.Parameters[1];
                Assert.Equal(("P", "x1 → payment(P), interest(P)"), (p.Name, p.Source));
                Assert.Equal("years", y.Name);

                // The slider: halfway is half the range; whole years only.
                p.SliderPosition = PanelParameter.SliderScale / 2;
                Assert.Equal(250000, loan.Value);
                y.SliderPosition = PanelParameter.SliderScale * 0.333;
                Assert.Equal(33, years.Value);

                // The box: exactly; past the slider, the slider reaches it; not a number, nothing changes.
                p.NumberText = "1234567.5";
                Assert.Equal(1234567.5, loan.Value);
                Assert.Equal(1234567.5, p.To);
                p.NumberText = "lots";
                Assert.Equal(1234567.5, loan.Value);

                // Renamed, and its range set.
                p.Name = "loan";
                p.FromText = "50000";
                p.ToText = "2000000";
                Assert.Equal(("loan", 50000.0, 2000000.0), (loan.PanelName, loan.PanelFrom, loan.PanelTo));

                // Named, it is its variable in the script too; emptied, named after the parameter again, and by the script.
                Assert.Contains("loan = Float64(1234567.5)", MainViewModel.ExportScript());
                p.Name = "P";
                Assert.Equal("P", loan.PanelName);
                p.Name = "";
                Assert.Equal(string.Empty, loan.PanelName);
                Assert.Equal("P", p.Name);
            });
        }

        [Fact]
        public void The_panel_is_saved_with_the_design_and_opens_with_it()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                OpenBareLoan(viewModel);
                Float loan = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).First();
                loan.InPanel = true;
                loan.PanelName = "loan";
                viewModel.SaveDesign(Path.Combine(_folder, "loan.jsynth"));

                viewModel.NewDesign();
                Assert.False(viewModel.IsParametersOpen);
                viewModel.OpenDesign(Path.Combine(_folder, "loan.jsynth"));
                Assert.True(viewModel.IsParametersOpen);
                Assert.Equal("loan", Assert.Single(viewModel.Parameters).Name);
            });
        }

        [Fact]
        public void Shown_in_the_window_the_slider_leaves_the_value_as_it_is()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.OpenExample(Loan);
                foreach (Node node in MainViewModel.Nodes.Where(n => n.CanBeInPanel))
                {
                    node.InPanel = true;
                }

                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1300, Height = 760 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(3, window.GetVisualDescendants().OfType<Slider>().Count(s => s.IsEffectivelyVisible && s.Maximum == PanelParameter.SliderScale));
                Assert.Equal([250000, 0.045], MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).Select(n => n.Value));
                Assert.Equal(30, MainViewModel.Nodes.OfType<Integer>().Single().Value);
                window.Close();
            });
        }

        [Fact]
        public void Real_julia_runs_the_design_again_a_moment_after_a_value_changes()
        {
            Ui.Run(async () =>
            {
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia)
                {
                    return;
                }

                MainViewModel.RunAfterChange = TimeSpan.FromMilliseconds(100);
                try
                {
                    MainViewModel viewModel = new() { Julia = julia };
                    OpenBareLoan(viewModel);
                    Integer years = MainViewModel.Nodes.OfType<Integer>().Single();
                    years.InPanel = true;
                    Assert.True(viewModel.RunOnChange);

                    // 15 years rather than 30: a larger monthly payment, shown under the panel.
                    viewModel.Parameters.Single().NumberText = "15";
                    Assert.True(await Wait.Until(() => viewModel.ParameterResults.Contains("payment = 1912.", StringComparison.Ordinal), 60),
                        viewModel.ParameterResults + "\n" + string.Join("\n", viewModel.Output.Select(o => o.Text)));

                    // Turned off: a change waits for a run, the values of the last no longer shown.
                    viewModel.RunOnChange = false;
                    viewModel.Parameters.Single().NumberText = "30";
                    await System.Threading.Tasks.Task.Delay(1000);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(string.Empty, viewModel.ParameterResults);
                    Assert.False(viewModel.IsRunning);
                    viewModel.EndJulia();
                }
                finally
                {
                    MainViewModel.RunAfterChange = TimeSpan.FromSeconds(0.5);
                }
            });
        }
    }
}
