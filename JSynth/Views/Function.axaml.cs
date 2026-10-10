using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JSynth.Controls;
using JSynth.ViewModels;

namespace JSynth.Views;

public partial class Function : InputsNode
{
    public Function()
    {
        InitializeComponent();
    }

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        OpenInPanel();
    }

    private void Node_DoubleTapped(object? sender, TappedEventArgs e)
    {
        OpenInPanel();
        e.Handled = true;
    }

    private void OpenInPanel()
    {
        if (DataContext is Model.Function function && TopLevel.GetTopLevel(this)?.DataContext is MainViewModel viewModel)
        {
            viewModel.Inspect(function);
        }
    }
}
