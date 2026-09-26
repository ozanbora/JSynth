using Newtonsoft.Json;
using ReactiveUI;
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
    }
}
