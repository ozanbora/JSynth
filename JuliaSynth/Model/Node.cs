using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;

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
