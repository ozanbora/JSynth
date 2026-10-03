using Avalonia.Interactivity;
using JuliaSynth.Controls;

namespace JuliaSynth.Views;

/// <summary>
/// The node going through a collection while carrying a state along.
/// </summary>
public partial class RunningNode : InputsNode
{
    public RunningNode()
    {
        InitializeComponent();
    }

    private void Keep_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.RunningNode running)
        {
            running.KeepEach = !running.KeepEach;
        }
    }
}
