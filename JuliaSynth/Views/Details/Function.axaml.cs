using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System.Linq;

namespace JuliaSynth.Views.Details;

public partial class Function : UserControl
{
    public Function()
    {
        InitializeComponent();

        // The body can use its parameters and call the design's other functions.
        BodyEditor.Completions = () => DataContext is Model.Function function
            ? JuliaCompletions.All(function, MainViewModel.Nodes.OfType<Model.Function>().Where(f => f != function))
            : [];
    }

    private void AddParameter_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as Model.Function)?.AddParameter();
    }

    private void RemoveParameter_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Parameter parameter })
        {
            (DataContext as Model.Function)?.RemoveParameter(parameter);
        }
    }
}
