using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.ObjectModel;

namespace JuliaSynth.Model
{
    public class Integer : NumericInput<long>
    {
        #region Private Properties
        private string _selectedType;
        #endregion

        #region Public Properties
        [JsonProperty(Order = 1)]
        public string SelectedType
        {
            get => _selectedType;
            set => this.RaiseAndSetIfChanged(ref _selectedType, value);
        }

        /// <summary>
        /// Julia type for the selected bitness, e.g. <c>Int32</c> for "32-bit".
        /// </summary>
        public override string OutputType => "Int" + SelectedType.Replace("-bit", string.Empty);

        public ReadOnlyObservableCollection<string> PossibleTypes { get; } = new([
            "8-bit",
            "16-bit",
            "32-bit",
            "64-bit",
        ]);
        #endregion

        public Integer() : base()
        {
            _selectedType = "32-bit";
            Color = Colors.DarkGreen;
            HardMax = long.MaxValue;
            HardMin = long.MinValue;
            Min = HardMin;
            Max = HardMax;
            Value = 0;

            _ = this.WhenAnyValue(x => x.SelectedType).Subscribe(SelectionChanged);
        }

        public void SelectionChanged(string x)
        {
            switch (SelectedType)
            {
                case "8-bit":
                    HardMax = sbyte.MaxValue;
                    HardMin = sbyte.MinValue;
                    break;

                case "16-bit":
                    HardMax = short.MaxValue;
                    HardMin = short.MinValue;
                    break;

                case "64-bit":
                    HardMax = long.MaxValue;
                    HardMin = long.MinValue;
                    break;

                default:
                    HardMax = int.MaxValue;
                    HardMin = int.MinValue;
                    break;
            }

            ResetRange();
            this.RaisePropertyChanged(nameof(OutputType));
        }

    }
}

