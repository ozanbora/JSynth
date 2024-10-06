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
        public T Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

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

        public T Min
        {
            get => _min;
            set => this.RaiseAndSetIfChanged(ref _min, value);
        }

        public T Max
        {
            get => _max;
            set => this.RaiseAndSetIfChanged(ref _max, value);
        }
        #endregion
        public NumericInput() : base()
        {
        }
    }
}
