using JSynth.Controls;

namespace JSynth.Views.Outputs;

/// <summary>
/// Shows the value wired in, as the last run gave it. Bigger than other nodes, to fit a vector or a matrix.
/// </summary>
public partial class DisplayNode : Node
{
    public DisplayNode() : base()
    {
        InitializeComponent();
        Width = 260;
        Height = 180;
    }
}
