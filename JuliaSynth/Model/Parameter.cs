using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A single argument of a <see cref="Function"/>, e.g. <c>x::Float64</c>.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Parameter : ReactiveObject
    {
        #region Private Properties
        private string _name;
        private string _type;
        #endregion

        #region Public Properties
        /// <summary>
        /// Stable identity, so a <see cref="Connection"/> keeps pointing at this parameter
        /// when it is renamed or reordered.
        /// </summary>
        [JsonProperty]
        public string ID { get; private set; } = Guid.NewGuid().ToString("N");

        [JsonProperty]
        public string Name
        {
            get => _name;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _name, value);
                this.RaisePropertyChanged(nameof(Signature));
            }
        }

        [JsonProperty]
        public string Type
        {
            get => _type;
            set
            {
                // A ComboBox writes back null when its selection is cleared (e.g. while its
                // items aren't bound yet); don't let that wipe the type.
                if (value is null)
                {
                    return;
                }

                _ = this.RaiseAndSetIfChanged(ref _type, value);
                this.RaisePropertyChanged(nameof(Signature));
            }
        }

        /// <summary>
        /// Julia representation of the parameter; untyped (Any) parameters have no annotation.
        /// </summary>
        public string Signature => Type == Function.AnyType ? Name : $"{Name}::{Type}";

        public ReadOnlyObservableCollection<string> PossibleTypes => Function.Types;
        #endregion

        public Parameter() : this("x", Function.AnyType)
        {
        }

        public Parameter(string name, string type)
        {
            _name = name;
            _type = type;
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
