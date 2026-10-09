using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using JuliaSynth.Runtime;

namespace JuliaSynth.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Node : ReactiveObject
    {
        #region Private Members
        private int _column;
        private int _row;
        private Color _color;
        private bool _isSelected;
        private Report? _result;
        private bool _isRunning;

        #endregion

        #region Public Properties
        [JsonProperty]
        public string ID { get; private set; }

        [JsonProperty]
        public int Column
        {
            get => _column;
            set => this.RaiseAndSetIfChanged(ref _column, value);
        }

        [JsonProperty]
        public int Row
        {
            get => _row;
            set => this.RaiseAndSetIfChanged(ref _row, value);
        }

        [JsonProperty]
        public Color Color
        {
            get => _color;
            set => this.RaiseAndSetIfChanged(ref _color, value);
        }

        /// <summary>
        /// A node read from a design saved before the pastel colours gets its kind's pastel, unless its
        /// colour was picked by hand.
        /// </summary>
        [System.Runtime.Serialization.OnDeserialized]
        private void Deserialized(System.Runtime.Serialization.StreamingContext context)
        {
            Color = NodePalette.Upgrade(this, Color);
        }

        /// <summary>
        /// Selected on the designer; not saved.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        /// <summary>
        /// What the last run of the design said about this node's value; not saved.
        /// </summary>
        public Report? Result
        {
            get => _result;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _result, value);
                foreach (string name in ResultProperties.Skip(1))
                {
                    this.RaisePropertyChanged(name);
                }

                OnResultChanged();
            }
        }

        #region The parameters panel
        private bool _inPanel;
        private string _panelName = string.Empty;
        private double? _panelFrom;
        private double? _panelTo;

        /// <summary>
        /// Whether this input can be set from the parameters panel: numbers, arrays typed in, folders.
        /// </summary>
        public virtual bool CanBeInPanel => false;

        /// <summary>
        /// Whether this input is set from the parameters panel too.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool InPanel
        {
            get => _inPanel;
            set => this.RaiseAndSetIfChanged(ref _inPanel, value);
        }

        /// <summary>
        /// Its name in the panel; empty for the name of the parameter it is wired into.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string PanelName
        {
            get => _panelName;
            set => this.RaiseAndSetIfChanged(ref _panelName, value ?? string.Empty);
        }

        /// <summary>
        /// Where a number's slider in the panel starts; null for a range around its value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? PanelFrom
        {
            get => _panelFrom;
            set => this.RaiseAndSetIfChanged(ref _panelFrom, value);
        }

        /// <summary>
        /// Where a number's slider in the panel ends; null for a range around its value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? PanelTo
        {
            get => _panelTo;
            set => this.RaiseAndSetIfChanged(ref _panelTo, value);
        }

        /// <summary>
        /// The properties of the panel: changing them doesn't change what the design works out.
        /// </summary>
        public static IReadOnlyList<string> PanelProperties { get; } = [nameof(InPanel), nameof(PanelName), nameof(PanelFrom), nameof(PanelTo)];
        #endregion

        private string _purpose = string.Empty;

        /// <summary>
        /// What an input is for, shown on it: its name in the parameters panel, else the name of the parameter it is wired
        /// into (as the panel calls it). Worked out from the wires (see MainViewModel.RefreshParameters); not saved.
        /// </summary>
        [JsonIgnore]
        public string Purpose
        {
            get => _purpose;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _purpose, value ?? string.Empty);
                this.RaisePropertyChanged(nameof(HasPurpose));
            }
        }

        [JsonIgnore]
        public bool HasPurpose => _purpose.Length > 0;

        private string _purposeTip = string.Empty;

        /// <summary>
        /// Where its value goes, in full, e.g. <c>payment(P), interest(P)</c>.
        /// </summary>
        [JsonIgnore]
        public string PurposeTip
        {
            get => _purposeTip;
            set => this.RaiseAndSetIfChanged(ref _purposeTip, value ?? string.Empty);
        }

        /// <summary>
        /// Being worked out in the run going on; not saved.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set => this.RaiseAndSetIfChanged(ref _isRunning, value);
        }

        /// <summary>
        /// Lets a node update what it shows of <see cref="Result"/>.
        /// </summary>
        protected virtual void OnResultChanged()
        {
        }

        /// <summary>
        /// Whether a change of <paramref name="property"/> only changes what is shown (the selection, the
        /// results of a run), not the design.
        /// </summary>
        public virtual bool IsViewState(string property)
        {
            return property is nameof(IsSelected) or nameof(IsRunning) or nameof(Purpose) or nameof(HasPurpose) or nameof(PurposeTip) || ResultProperties.Contains(property);
        }

        /// <summary>
        /// <see cref="Result"/> and the properties shown from it: changing them doesn't change the design.
        /// </summary>
        public static IReadOnlyList<string> ResultProperties { get; } = [
            nameof(Result), nameof(HasResult), nameof(IsResultOk), nameof(IsResultError), nameof(IsResultSkipped), nameof(ResultTip), nameof(ResultTime), nameof(TimeLabel),
        ];

        public bool HasResult => Result is not null;

        public bool IsResultOk => Result?.Status == ReportStatus.Ok;

        public bool IsResultError => Result?.Status == ReportStatus.Error;

        public bool IsResultSkipped => Result?.Status == ReportStatus.Skipped;

        /// <summary>
        /// The value and its type, or what went wrong; and how long it took.
        /// </summary>
        public string ResultTip => Result switch
        {
            { Status: ReportStatus.Ok } r => $"{r.Type}\n{r.Text}{Took}",
            { Status: ReportStatus.Error } r => $"Error\n{r.Text}{Took}",
            { Status: ReportStatus.Skipped } r => $"Not run\n{r.Text}",
            _ => string.Empty,
        };

        private string Took => ResultTime.Length == 0 ? string.Empty : $"\n\nTook {ResultTime}";

        /// <summary>
        /// How long working the value out took (going through files, for all of them so far), e.g. <c>12 ms</c>; empty
        /// when it wasn't timed.
        /// </summary>
        public string ResultTime => Result?.Seconds is double seconds ? Duration(seconds) : string.Empty;

        /// <summary>
        /// <see cref="ResultTime"/> shown on the node: only when it took a millisecond or more, so the many quick ones
        /// don't crowd the canvas.
        /// </summary>
        public string TimeLabel => Result?.Seconds >= 0.001 ? ResultTime : string.Empty;

        /// <summary>
        /// <paramref name="seconds"/> as a short time: <c>0.4 ms</c>, <c>12 ms</c>, <c>1.3 s</c>, <c>2 min 5 s</c>.
        /// </summary>
        public static string Duration(double seconds) => seconds switch
        {
            < 0.01 => FormattableString.Invariant($"{seconds * 1000:0.0} ms"),
            < 1 => FormattableString.Invariant($"{seconds * 1000:0} ms"),
            < 60 => FormattableString.Invariant($"{seconds:0.0} s"),
            _ => FormattableString.Invariant($"{Math.Floor(seconds / 60)} min {seconds % 60:0} s"),
        };

        /// <summary>
        /// Julia type of the value this node outputs, e.g. <c>Float32</c>.
        /// </summary>
        public virtual string OutputType => Function.AnyType;

        /// <summary>
        /// The Julia type of <paramref name="output"/>: <see cref="OutputType"/> for the node's value (null), or that of
        /// another of its outputs, e.g. a Merge node's folder.
        /// </summary>
        public virtual string OutputTypeOf(string? output) => OutputType;

        /// <summary>
        /// False for the nodes at the end of a design (showing or saving a value), which nothing can be wired from.
        /// </summary>
        public virtual bool HasOutput => true;

        /// <summary>
        /// The parameters wires can feed: a function's parameters, an array builder's sizes, ...
        /// Observable when they can change.
        /// </summary>
        public virtual IReadOnlyList<Parameter> Inputs => [];

        /// <summary>
        /// <see cref="Inputs"/>, plus those kept for later that aren't used right now.
        /// </summary>
        public virtual IEnumerable<Parameter> AllInputs => Inputs;

        /// <summary>
        /// How many rows it takes under its header, for its connectors: one an input (a group's, also one an output after
        /// its value).
        /// </summary>
        public virtual int ConnectorRows => Inputs.Count;

        /// <summary>
        /// The port of an input node taking its value for the next file, in a design going through files one
        /// at a time (a Files node so set, or a Watch folder): the value it starts with is the one set on it,
        /// and after each file it takes the value wired here, as <c>C = update(C, X)</c> in a loop. Null for
        /// the nodes that compute their value from others.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Parameter? Next { get; protected set; }

        /// <summary>
        /// Every port wires can end at: <see cref="Inputs"/>, and <see cref="Next"/>.
        /// </summary>
        public IEnumerable<Parameter> Ports => Next is null ? Inputs : Inputs.Append(Next);

        #endregion

        /// <summary>
        /// A wire into it was made, <paramref name="connections"/> being the design's wires now: lets a node with
        /// a port for each of a kind of value add another.
        /// </summary>
        public virtual void Wired(IEnumerable<Connection> connections)
        {
        }

        public Node()
        {
            ID = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Gives a pasted copy an identity of its own.
        /// </summary>
        internal void RenewID()
        {
            ID = Guid.NewGuid().ToString("N");
        }
    }
}
