using Avalonia.Media;
using JSynth.Runtime;
using Newtonsoft.Json;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
{
    /// <summary>
    /// Plots a value: a vector as a line or points (against x values if wired), a matrix as a heatmap or,
    /// sparse, as dots where its values are. The numbers come back from the run (see <see cref="PlotData"/>).
    /// </summary>
    public class PlotNode : Node
    {
        public const string Auto = "Auto";

        public static IReadOnlyList<string> Kinds { get; } = [Auto, "Line", "Scatter", "Heatmap", "Spy"];

        private static readonly string[] ShownProperties = [nameof(Plot), nameof(Message)];

        private string _kind = Auto;
        private string _title = string.Empty;
        private PlotData? _plot;

        /// <summary>
        /// The values to plot: a vector or a matrix.
        /// </summary>
        [JsonProperty]
        public Parameter Y { get; private set; } = new("y", Function.AnyType);

        /// <summary>
        /// Optional x values for a line or points; without them, y is plotted against 1, 2, 3, ...
        /// </summary>
        [JsonProperty]
        public Parameter X { get; private set; } = new("x", JuliaTypes.AbstractVector);

        [JsonProperty]
        public string Kind
        {
            get => _kind;
            set
            {
                if (value is not null && Kinds.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _kind, value);
                }
            }
        }

        /// <summary>
        /// Shown above the plot; nothing when empty.
        /// </summary>
        [JsonProperty]
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value ?? string.Empty);
        }

        public override IReadOnlyList<Parameter> Inputs => [Y, X];

        public override bool HasOutput => false;

        /// <summary>
        /// The numbers from the last run; not saved.
        /// </summary>
        public PlotData? Plot
        {
            get => _plot;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _plot, value);
                this.RaisePropertyChanged(nameof(Message));
            }
        }

        /// <summary>
        /// Shown instead of a plot: what to do to get one, or what went wrong.
        /// </summary>
        public string? Message => Plot is not null ? null : Result switch
        {
            { Status: ReportStatus.Error } r => r.Text,
            { Status: ReportStatus.Skipped } r => $"Not run: {r.Text}",
            { Status: ReportStatus.Ok } => "Reading the plot…",
            _ => "Run the design (F5) to see the plot here.",
        };

        public PlotNode()
        {
            Color = NodePalette.Plot;
        }

        /// <summary>
        /// Julia code writing the plot of <paramref name="y"/> (against <paramref name="x"/>, when wired) to
        /// <paramref name="path"/>.
        /// </summary>
        public string Julia(string path, string y, string? x)
        {
            return JuliaAt(ArrayFile.PathLiteral(path), y, x);
        }

        /// <summary>
        /// As <see cref="Julia"/>, the file being a Julia expression: one of its own each time a stream draws it.
        /// </summary>
        public string JuliaAt(string path, string y, string? x)
        {
            return $"_jsynth_plot({path}, \"{Kind.ToLowerInvariant()}\", {y}{(x is null ? string.Empty : $", {x}")})";
        }

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || ShownProperties.Contains(property);
        }

        protected override void OnResultChanged()
        {
            // A new result (or none) makes the plot of the last one wrong; the run sets the new plot.
            if (Result?.Status != ReportStatus.Ok)
            {
                Plot = null;
            }

            this.RaisePropertyChanged(nameof(Message));
        }
    }
}
