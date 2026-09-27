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
            }
        }

        /// <summary>
        /// <see cref="Result"/> and the properties shown from it: changing them doesn't change the design.
        /// </summary>
        public static IReadOnlyList<string> ResultProperties { get; } = [
            nameof(Result), nameof(HasResult), nameof(IsResultOk), nameof(IsResultError), nameof(IsResultSkipped), nameof(ResultTip),
        ];

        public bool HasResult => Result is not null;

        public bool IsResultOk => Result?.Status == ReportStatus.Ok;

        public bool IsResultError => Result?.Status == ReportStatus.Error;

        public bool IsResultSkipped => Result?.Status == ReportStatus.Skipped;

        /// <summary>
        /// The value and its type, or what went wrong.
        /// </summary>
        public string ResultTip => Result switch
        {
            { Status: ReportStatus.Ok } r => $"{r.Type}\n{r.Text}",
            { Status: ReportStatus.Error } r => $"Error\n{r.Text}",
            { Status: ReportStatus.Skipped } r => $"Not run\n{r.Text}",
            _ => string.Empty,
        };

        /// <summary>
        /// Julia type of the value this node outputs, e.g. <c>Float32</c>.
        /// </summary>
        public virtual string OutputType => Function.AnyType;

        /// <summary>
        /// The parameters wires can feed: a function's parameters, an array builder's sizes, ...
        /// Observable when they can change.
        /// </summary>
        public virtual IReadOnlyList<Parameter> Inputs => [];

        /// <summary>
        /// <see cref="Inputs"/>, plus those kept for later that aren't used right now.
        /// </summary>
        public virtual IEnumerable<Parameter> AllInputs => Inputs;

        #endregion
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
