using Newtonsoft.Json;
using ReactiveUI;
using System.Numerics;

namespace JuliaSynth.Model
{
    public class NumericInput<T> : Node where T : INumber<T>
    {
        #region Private Properties
        private T _value = T.Zero;
        private T _hardMin = T.Zero;
        private T _hardMax = T.One;
        private T _min = T.Zero;
        private T _max = T.One;
        #endregion

        #region Public Properties
        // Serialization order matters: derived classes serialize SelectedType with Order = 1,
        // whose setter resets Min/Max to the hard limits. Min/Max/Value must be applied after it.
        [JsonProperty(Order = 3)]
        public T Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        // HardMin/HardMax are derived from the selected bitness, so they are not serialized.
        public T HardMin
        {
            get => _hardMin;
            set => this.RaiseAndSetIfChanged(ref _hardMin, value);
        }

        public T HardMax
        {
            get => _hardMax;
            set => this.RaiseAndSetIfChanged(ref _hardMax, value);
        }

        [JsonProperty(Order = 2)]
        public T Min
        {
            get => _min;
            set => this.RaiseAndSetIfChanged(ref _min, value);
        }

        [JsonProperty(Order = 2)]
        public T Max
        {
            get => _max;
            set => this.RaiseAndSetIfChanged(ref _max, value);
        }
        #endregion

        public override bool CanBeInPanel => true;

        public NumericInput() : base()
        {
            Next = new Parameter("next", Function.AnyType);
        }

        /// <summary>
        /// Resets Min/Max to the hard limits and clamps Value into the new range.
        /// Called by derived classes when the bitness changes.
        /// </summary>
        protected void ResetRange()
        {
            Min = HardMin;
            Max = HardMax;
            Value = T.Clamp(Value, Min, Max);
        }
    }
}
