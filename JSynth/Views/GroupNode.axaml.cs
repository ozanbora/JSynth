using Avalonia.Input;
using Avalonia.Interactivity;
using JSynth.Controls;
using JSynth.ViewModels;

namespace JSynth.Views;

/// <summary>
/// Nodes grouped into one; double-clicked, opened: its nodes back on the canvas.
/// </summary>
public partial class GroupNode : InputsNode
{
    public GroupNode()
    {
        InitializeComponent();
    }

    private void Open()
    {
        if (DataContext is Model.GroupNode group)
        {
            MainViewModel.OpenGroup(group);
        }
    }

    private void Group_DoubleTapped(object? sender, TappedEventArgs e)
    {
        Open();
        e.Handled = true;
    }

    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        Open();
    }
}
