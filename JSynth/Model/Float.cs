using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.ObjectModel;

namespace JSynth.Model
{
    public class Float : NumericInput<double>
    {
        #region Private Properties
        private string _selectedType;

        private double _epsilon;
        #endregion

        #region Public Properties
        [JsonProperty(Order = 1)]
        public string SelectedType
        {
            get => _selectedType;
            set => this.RaiseAndSetIfChanged(ref _selectedType, value);
        }

        /// <summary>
        /// Julia's eps(T): the gap between 1.0 and the next number of the selected type. Not .NET's float.Epsilon,
        /// which is the smallest number above zero.
        /// </summary>
        public double Epsilon
        {
            get => _epsilon;
            set => this.RaiseAndSetIfChanged(ref _epsilon, value);
        }

        /// <summary>
        /// Julia type for the selected bitness, e.g. <c>Float32</c> for "32-bit".
        /// </summary>
        public override string OutputType => "Float" + SelectedType.Replace("-bit", string.Empty);

        public ReadOnlyObservableCollection<string> PossibleTypes { get; } = new([
            "16-bit",
            "32-bit",
            "64-bit",
        ]);
        #endregion

        public Float() : base()
        {
            _selectedType = "32-bit";
            Color = NodePalette.Float;
            HardMax = float.MaxValue;
            HardMin = float.MinValue;
            Min = HardMin;
            Max = HardMax;
            Value = 0.0;
            Epsilon = Math.ScaleB(1, -23);

            _ = this.WhenAnyValue(x => x.SelectedType).Subscribe(SelectionChanged);
        }

        private void SelectionChanged(string x)
        {
            switch (SelectedType)
            {
                case "16-bit":
                    HardMax = (double)Half.MaxValue;
                    HardMin = (double)Half.MinValue;
                    Epsilon = Math.ScaleB(1, -10);
                    break;

                case "64-bit":
                    HardMax = double.MaxValue;
                    HardMin = double.MinValue;
                    Epsilon = Math.ScaleB(1, -52);
                    break;

                default:
                    HardMax = float.MaxValue;
                    HardMin = float.MinValue;
                    Epsilon = Math.ScaleB(1, -23);
                    break;
            }

            ResetRange();
            this.RaisePropertyChanged(nameof(OutputType));
        }
    }
}
