using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.Controls;
using JuliaSynth.ViewModels;

namespace JuliaSynth.Views;

/// <summary>
/// The button: the design runs as a stream, the nodes after it again at each press.
/// </summary>
public partial class ButtonNode : InputsNode
{
    public ButtonNode()
    {
        InitializeComponent();
    }

    private void Press_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.ButtonNode button && TopLevel.GetTopLevel(this)?.DataContext is MainViewModel viewModel)
        {
            viewModel.Press(button);
        }
    }
}
