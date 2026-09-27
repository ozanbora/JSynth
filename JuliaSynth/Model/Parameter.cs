using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;

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
        private bool _isWired;
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
                this.RaisePropertyChanged(nameof(Shape));
                this.RaisePropertyChanged(nameof(ElementType));
            }
        }

        /// <summary>
        /// Scalar, Vector or Matrix: the first half of <see cref="Type"/>, picked separately.
        /// </summary>
        public string Shape
        {
            get => JuliaTypes.Split(Type).Shape;
            set
            {
                if (value is not null)
                {
                    Type = JuliaTypes.Compose(value, ElementType);
                }
            }
        }

        /// <summary>
        /// Any, Float64, ...: the second half of <see cref="Type"/>.
        /// </summary>
        public string ElementType
        {
            get => JuliaTypes.Split(Type).ElementType;
            set
            {
                if (value is not null)
                {
                    Type = JuliaTypes.Compose(Shape, value);
                }
            }
        }

        /// <summary>
        /// Julia representation of the parameter; untyped (Any) parameters have no annotation.
        /// </summary>
        public string Signature => Type == Function.AnyType ? Name : $"{Name}::{Type}";

        /// <summary>
        /// Whether a wire feeds this parameter; kept up to date by the designer, not saved.
        /// </summary>
        public bool IsWired
        {
            get => _isWired;
            set => this.RaiseAndSetIfChanged(ref _isWired, value);
        }

        public static IReadOnlyList<string> Shapes => JuliaTypes.Shapes;

        public static IReadOnlyList<string> ElementTypes => JuliaTypes.ElementTypes;
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
