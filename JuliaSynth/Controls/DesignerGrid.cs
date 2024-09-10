using Avalonia.Controls;

namespace JuliaSynth.Controls
{
    public class DesignerGrid : Grid
    {
        private readonly int NumberOfRows = 100;
        private readonly int NumberOfColumns = 100;

        private new readonly GridLength Height = new(50);
        private new readonly GridLength Width = new(50);

        static DesignerGrid()
        {

        }
        public DesignerGrid() : base()
        {
            RowDefinitions = []; ColumnDefinitions = [];

            for (int i = 0; i < NumberOfRows; i++)
            {
                RowDefinitions.Add(new RowDefinition() { Height = Height });
            }

            for (int i = 0; i < NumberOfColumns; i++)
            {
                ColumnDefinitions.Add(new ColumnDefinition() { Width = Width });
            }

            MinWidth = NumberOfColumns * Width.Value;
            MaxHeight = NumberOfRows * Height.Value;
        }
    }
}