using JuliaSynth.Controls;

namespace JuliaSynth.Views;

/// <summary>
/// The clock: the design runs as a stream, ticking every so many seconds.
/// </summary>
public partial class EveryNode : InputsNode
{
    public EveryNode()
    {
        InitializeComponent();
    }
}
