using Avalonia.Controls;

namespace JuliaSynth.Controls;

/// <summary>
/// Shows on a node what the last run gave for it; the value or error is in the tooltip.
/// </summary>
public partial class ResultBadge : UserControl
{
    public ResultBadge()
    {
        InitializeComponent();
    }
}
