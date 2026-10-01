using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.ObjectModel;

namespace JuliaSynth.Model
{
    public class UnsignedInteger : NumericInput<UInt128>
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
        /// Julia type for the selected bitness, e.g. <c>UInt32</c> for "32-bit".
        /// </summary>
        public override string OutputType => "UInt" + SelectedType.Replace("-bit", string.Empty);

        public ReadOnlyObservableCollection<string> PossibleTypes { get; } = new([
            "8-bit",
            "16-bit",
            "32-bit",
            "64-bit",
        ]);

        #endregion
        public UnsignedInteger() : base()
        {
            _selectedType = "32-bit";
            Color = NodePalette.Unsigned;
            HardMax = uint.MaxValue;
            HardMin = uint.MinValue;
            Min = HardMin;
            Max = HardMax;
            Value = Min;
            _ = this.WhenAnyValue(x => x.SelectedType).Subscribe(SelectionChanged);
        }

        public void SelectionChanged(string x)
        {
            switch (SelectedType)
            {
                case "8-bit":
                    HardMax = byte.MaxValue;
                    HardMin = byte.MinValue;
                    break;

                case "16-bit":
                    HardMax = ushort.MaxValue;
                    HardMin = ushort.MinValue;
                    break;

                case "64-bit":
                    HardMax = ulong.MaxValue;
                    HardMin = ulong.MinValue;
                    break;

                default:
                    HardMax = uint.MaxValue;
                    HardMin = uint.MinValue;
                    break;
            }

            ResetRange();
            this.RaisePropertyChanged(nameof(OutputType));
        }
    }
}
