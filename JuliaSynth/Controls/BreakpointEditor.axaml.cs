using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.ViewModels;

namespace JuliaSynth.Controls;

/// <summary>
/// The card opening from a breakpoint's dot on a wire.
/// </summary>
public partial class BreakpointEditor : UserControl
{
    public BreakpointEditor()
    {
        InitializeComponent();
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as BreakpointEditing)?.Remove();
    }
}
