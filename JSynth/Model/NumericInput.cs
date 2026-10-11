using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Globalization;
using System.Numerics;

namespace JSynth.Model
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

        /// <summary>
        /// The step its slider in the parameters panel moves in: the one set (<see cref="Node.PanelStep"/>), else the
        /// default for its range, its limits. Not saved: worked out.
        /// </summary>
        [JsonIgnore]
        public double Step => PanelStep ?? SliderStep.Default(double.CreateSaturating(Min), double.CreateSaturating(Max), IsWhole);

        /// <summary>
        /// <see cref="Step"/> as typed in its editor; emptied, the default again.
        /// </summary>
        [JsonIgnore]
        public string StepText
        {
            get => Step.ToString("G", CultureInfo.InvariantCulture);
            set
            {
                if (SliderStep.TryParse(value, IsWhole, out double? step))
                {
                    PanelStep = step;
                }

                this.RaisePropertyChanged();
            }
        }

        private static bool IsWhole => typeof(T) != typeof(double);

        public override bool CanBeInPanel => true;

        public NumericInput() : base()
        {
            Next = new Parameter("next", Function.AnyType);

            // The step follows the one set, and the default the range. (WhenAnyValue can't be generated for a generic type.)
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(PanelStep) or nameof(Min) or nameof(Max))
                {
                    this.RaisePropertyChanged(nameof(Step));
                    this.RaisePropertyChanged(nameof(StepText));
                }
            };
        }

        // Worked out from what is saved: not changes of their own.
        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property is nameof(Step) or nameof(StepText);
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
