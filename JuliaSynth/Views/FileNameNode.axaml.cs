using Avalonia.Interactivity;
using JuliaSynth.Controls;

namespace JuliaSynth.Views;

/// <summary>
/// The node giving the name, or the whole path, of the file a loop is at.
/// </summary>
public partial class FileNameNode : Node
{
    public FileNameNode() : base()
    {
        InitializeComponent();
    }

    private void Toggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.FileNameNode name)
        {
            name.WholePath = !name.WholePath;
        }
    }
}
