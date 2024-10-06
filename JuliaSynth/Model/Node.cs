using Avalonia.Media;
using ReactiveUI;
using System;

namespace JuliaSynth.Model
{
    public class Node : ReactiveObject
    {
        #region Private Members
        private int _column;
        private int _row;
        private Color _color;

        #endregion

        #region Public Properties
        public string ID { get; private set; }

        public int Column
        {
            get => _column;
            set => this.RaiseAndSetIfChanged(ref _column, value);
        }

        public int Row
        {
            get => _row;
            set => this.RaiseAndSetIfChanged(ref _row, value);
        }

        public Color Color
        {
            get => _color;
            set => this.RaiseAndSetIfChanged(ref _color, value);
        }

        #endregion
        public Node()
        {
            ID = Guid.NewGuid().ToString("N");
        }
    }
}
