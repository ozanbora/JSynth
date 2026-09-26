using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.Model;

namespace JuliaSynth.Views.Details;

public partial class Function : UserControl
{
    public Function()
    {
        InitializeComponent();
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
