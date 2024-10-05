using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace JuliaSynth.ViewModels.Inputs
{
    public class UnsignedIntegerViewModel : ReactiveObject
    {
        #region Private Properties
        private Color _color;

        private string _selectedType;

        private UInt128 _min;

        private UInt128 _max;

        private UInt128 _hardMax;

        private UInt128 _hardMin;

        private UInt128 _value;
        #endregion

        #region Public Properties
        public string SelectedType
        {
            get => _selectedType;
            set => this.RaiseAndSetIfChanged(ref _selectedType, value);
        }

        public ReadOnlyObservableCollection<string> PossibleTypes { get; } = new([
            "8-bit",
            "16-bit",
            "32-bit",
            "64-bit",
        ]);

        [Required]
        public UInt128 HardMax
        {
            get => _hardMax;
            set => this.RaiseAndSetIfChanged(ref _hardMax, value);
        }

        [Required]
        public UInt128 HardMin
        {
            get => _hardMin;
            set => this.RaiseAndSetIfChanged(ref _hardMin, value);
        }

        [Required]
        public UInt128 Min
        {
            get => _min;
            set => this.RaiseAndSetIfChanged(ref _min, value);
        }

        [Required]
        public UInt128 Max
        {
            get => _max;
            set => this.RaiseAndSetIfChanged(ref _max, value);
        }

        [Required]
        public UInt128 Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public Color Color
        {
            get => _color;
            set => this.RaiseAndSetIfChanged(ref _color, value);
        }

        #endregion
        public UnsignedIntegerViewModel()
        {
            _selectedType = "32-bit";
            Color = Colors.DarkBlue;
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

            Min = HardMin;
            Max = HardMax;
        }

    }
}
