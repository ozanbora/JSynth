using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JSynth.Model;
using JSynth.Runtime;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// The parameters panel: inputs set with sliders and boxes, the design running again a moment after each change.
    /// </summary>
    public sealed class ParametersPanelTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));
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
        /// The Loan payment example with nothing in the panel, its numbers with no limits but their types', and the panel
        /// closed.
        /// </summary>
        private static void OpenBareLoan(MainViewModel viewModel)
        {
            viewModel.OpenExample(Loan);
            foreach (Node node in MainViewModel.Nodes)
            {
                (node.InPanel, node.PanelName, node.PanelStep) = (false, string.Empty, null);
                switch (node)
                {
                    case Float f:
                        (f.Min, f.Max) = (f.HardMin, f.HardMax);
                        break;
                    case Integer z:
                        (z.Min, z.Max) = (z.HardMin, z.HardMax);
                        break;
                }
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

                // With no limits of their own, they got ones around their values: the sliders' ranges.
                Assert.Equal((0.0, 500000.0), (loan.Min, loan.Max));
                Assert.Equal((0L, 100L), (years.Min, years.Max));
                Assert.Equal((loan.Min, loan.Max), (p.From, p.To));

                // The slider: halfway is half the range; whole years only.
                p.SliderPosition = PanelParameter.SliderScale / 2;
                Assert.Equal(250000, loan.Value);
                y.SliderPosition = PanelParameter.SliderScale * 0.333;
                Assert.Equal(33, years.Value);

                // The box: exactly; past the limits, held at them; not a number, nothing changes.
                p.NumberText = "123456.5";
                Assert.Equal(123456.5, loan.Value);
                p.NumberText = "1234567.5";
                Assert.Equal(500000, loan.Value);
                p.NumberText = "lots";
                Assert.Equal(500000, loan.Value);

                // Renamed, and its range set: the number's limits.
                p.Name = "loan";
                p.FromText = "50000";
                p.ToText = "2000000";
                Assert.Equal(("loan", 50000.0, 2000000.0), (loan.PanelName, loan.Min, loan.Max));
                p.NumberText = "1234567.5";
                Assert.Equal(1234567.5, loan.Value);

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
        public void A_slider_steps_by_a_hundredth_of_its_range_rounded_down_and_by_whole_numbers_for_integers()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.OpenExample(Loan);

                // loan 10,000 to 1,000,000; rate 0 to 0.15; years 1 to 40.
                Assert.Equal([5000, 0.001, 1], viewModel.Parameters.Select(p => p.Step));
            });
        }

        /// <summary>
        /// The loan example's rate alone in the panel, its slider from <paramref name="from"/> to 1.
        /// </summary>
        private static (Float Rate, PanelParameter Panel) RateFrom(MainViewModel viewModel, double from)
        {
            OpenBareLoan(viewModel);
            Float rate = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).Last();
            (rate.InPanel, rate.Min, rate.Max) = (true, from, 1);
            return (rate, viewModel.Parameters.Single());
        }

        [Fact]
        public void The_slider_lands_on_steps_from_its_start_exactly_and_an_arrow_key_moves_one()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                (Float rate, PanelParameter p) = RateFrom(viewModel, 0);
                p.StepText = "0.05";
                Assert.Equal(0.05, rate.PanelStep);

                p.SliderPosition = 0.3471 * PanelParameter.SliderScale;
                Assert.Equal(0.35, rate.Value);

                // 0.3, not 0.30000000000000004 (6 times 0.05 in double).
                p.SliderPosition = 0.3 * PanelParameter.SliderScale;
                Assert.Equal(0.3, rate.Value);

                p.SliderPosition += p.SliderSmallChange;
                Assert.Equal(0.35, rate.Value);
                p.SliderPosition += p.SliderLargeChange;
                Assert.Equal(0.85, rate.Value);

                // Steps count from the start of the range, and stop at its end. A fifth of the way along 0.03 to 1 is
                // 0.194 past the start; the nearest step is 0.2 past it.
                rate.Min = 0.03;
                p.SliderPosition = 0.2 * PanelParameter.SliderScale;
                Assert.Equal(0.23, rate.Value);
                p.SliderPosition = PanelParameter.SliderScale;
                Assert.Equal(0.98, rate.Value);
            });
        }

        [Fact]
        public void An_integers_step_is_whole_and_at_least_one_and_emptied_is_the_default_again()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                OpenBareLoan(viewModel);
                Integer years = MainViewModel.Nodes.OfType<Integer>().Single();
                (years.InPanel, years.Min, years.Max) = (true, 1, 40);
                PanelParameter p = viewModel.Parameters.Single();

                p.StepText = "0.5";
                Assert.Equal(1, p.Step);
                p.StepText = "2.6";
                Assert.Equal(3, p.Step);
                p.StepText = "-2";
                Assert.Equal(3, p.Step);
                p.StepText = "lots";
                Assert.Equal(3, p.Step);

                p.SliderPosition = PanelParameter.SliderScale / 2;
                Assert.Equal(19, years.Value);

                p.StepText = "";
                Assert.Null(years.PanelStep);
                Assert.Equal(1, p.Step);
            });
        }

        [Fact]
        public void A_typed_value_is_kept_as_typed_and_shown_cut_off_at_the_steps_decimals()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                (Float rate, PanelParameter p) = RateFrom(viewModel, 0);
                p.StepText = "0.05";

                p.NumberText = "0.379";
                Assert.Equal(0.379, rate.Value);
                Assert.Equal("0.37", p.NumberText);
                Assert.Equal("0.379", p.NumberTip);

                // The box gives back what it shows when it loses focus: that isn't a new value.
                p.NumberText = "0.37";
                Assert.Equal(0.379, rate.Value);

                p.NumberText = "0.36";
                Assert.Equal(0.36, rate.Value);
                Assert.Null(p.NumberTip);
            });
        }

        [Theory]
        [InlineData(0.57, 2, "0.57")]
        [InlineData(2.379, 2, "2.37")]
        [InlineData(-2.379, 2, "-2.37")]
        [InlineData(250000.0, 0, "250000")]
        [InlineData(0.3, 1, "0.3")]
        public void A_number_is_cut_off_not_rounded(double value, int decimals, string shown)
        {
            Assert.Equal(shown, PanelParameter.Truncated(value, decimals));
        }

        [Fact]
        public void The_step_is_saved_with_the_design_and_undone_like_the_range()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                (Float rate, PanelParameter p) = RateFrom(viewModel, 0);
                MainViewModel.History.Commit();
                p.StepText = "0.05";
                MainViewModel.History.Commit();

                MainViewModel.History.Undo();
                Assert.Null(MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).Last().PanelStep);
                MainViewModel.History.Redo();

                viewModel.SaveDesign(Path.Combine(_folder, "rate.jsynth"));
                viewModel.NewDesign();
                viewModel.OpenDesign(Path.Combine(_folder, "rate.jsynth"));
                Assert.Equal(0.05, MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).Last().PanelStep);
            });
        }

        [Fact]
        public void In_the_window_an_arrow_key_on_a_slider_moves_the_value_one_step()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                (Float rate, PanelParameter p) = RateFrom(viewModel, 0);
                p.StepText = "0.05";
                p.NumberText = "0.5";

                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1300, Height = 760 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Slider slider = window.GetVisualDescendants().OfType<Slider>().Single(s => s.IsEffectivelyVisible);
                slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0.55, rate.Value);

                slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
                slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0.45, rate.Value);
                window.Close();
            });
        }

        [Fact]
        public void The_sliders_range_is_the_numbers_min_and_max_both_ways()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.OpenExample(Loan);
                Float loan = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).First();
                Integer years = MainViewModel.Nodes.OfType<Integer>().Single();
                PanelParameter p = viewModel.Parameters[0];
                PanelParameter y = viewModel.Parameters[2];

                // The example's range on the node and in the panel alike.
                Assert.Equal((10_000.0, 1_000_000.0), (loan.Min, loan.Max));
                Assert.Equal((loan.Min, loan.Max), (p.From, p.To));

                // Set in the panel: the node's limits; narrowed past the value: the value moves into the range.
                p.ToText = "200000";
                Assert.Equal((10_000.0, 200_000.0), (loan.Min, loan.Max));
                Assert.Equal(200_000, loan.Value);

                // Set on the node: the panel's range.
                loan.Min = 50_000;
                Assert.Equal(("50000", 50_000.0), (p.FromText, p.From));

                // An integer's range is whole: 1.5 to 9.5 is 2 to 9.
                y.FromText = "1.5";
                y.ToText = "9.5";
                Assert.Equal((2L, 9L), (years.Min, years.Max));

                // A number with no limits but its type's: the slider goes around the value it had when put in the panel
                // (250,000), fixed while it is shown.
                (loan.Min, loan.Max) = (loan.HardMin, loan.HardMax);
                Assert.Equal((0, 500_000), (p.From, p.To));
            });
        }

        [Fact]
        public void The_editors_step_is_the_panels_both_ways_and_keeps_the_results()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                viewModel.OpenExample(Loan);
                Float rate = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).Skip(1).First();
                Integer years = MainViewModel.Nodes.OfType<Integer>().Single();
                PanelParameter r = viewModel.Parameters[1];
                rate.Result = new Report(rate.ID, ReportStatus.Ok, "Float64", "0.045");

                // The default, from the same range: 0 to 0.15.
                Assert.Equal(("0.001", "0.001"), (rate.StepText, r.StepText));

                // Set in the editor: the panel's; set in the panel: the editor's.
                rate.StepText = "0.005";
                Assert.Equal((0.005, 0.005), (r.Step, rate.PanelStep));
                r.StepText = "0.01";
                Assert.Equal("0.01", rate.StepText);
                Assert.NotNull(rate.Result);

                // An integer's: whole, at least 1; emptied: the default again.
                years.StepText = "0.4";
                Assert.Equal(1, years.Step);
                rate.StepText = "";
                Assert.Equal((null, "0.001"), (rate.PanelStep, rate.StepText));
            });
        }

        [Fact]
        public void A_design_opened_with_a_number_in_the_panel_without_limits_gets_them_and_stays_unchanged()
        {
            Ui.Run(() =>
            {
                MainViewModel viewModel = new();
                OpenBareLoan(viewModel);
                Float loan = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).First();
                loan.InPanel = true;

                // Saved with no limits but Float64's, as a bitness changed afterwards leaves it.
                (loan.Min, loan.Max) = (loan.HardMin, loan.HardMax);
                viewModel.SaveDesign(Path.Combine(_folder, "unlimited.jsynth"));

                viewModel.NewDesign();
                viewModel.OpenDesign(Path.Combine(_folder, "unlimited.jsynth"));
                Float opened = MainViewModel.Nodes.OfType<Float>().OrderBy(n => n.Row).First();
                PanelParameter p = viewModel.Parameters.Single();

                Assert.Equal((0.0, 500000.0), (opened.Min, opened.Max));
                Assert.Equal((opened.Min, opened.Max), (p.From, p.To));
                Assert.False(viewModel.IsDirty);

                // A new bitness resets its limits to the type's: in the panel, it gets ones around its value again.
                opened.SelectedType = "32-bit";
                Assert.Equal((0.0, 500000.0), (opened.Min, opened.Max));
                Assert.Equal((opened.Min, opened.Max), (p.From, p.To));
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
