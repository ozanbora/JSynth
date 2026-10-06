using Avalonia.Interactivity;
using JuliaSynth.Controls;

namespace JuliaSynth.Views;

/// <summary>
/// The node applying a function to each value of a collection.
/// </summary>
public partial class MapNode : InputsNode
{
    public MapNode()
    {
        InitializeComponent();
    }

    private void Parallel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.MapNode map)
        {
            map.InParallel = !map.InParallel;
        }
    }
}
