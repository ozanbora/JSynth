using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.ViewModels;

namespace JuliaSynth.Views;

/// <summary>
/// The sidebar: its rows do what the File menu does, through the window, which asks about unsaved changes first.
/// </summary>
public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private MainWindow? Window => TopLevel.GetTopLevel(this) as MainWindow;

    private async void New_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window)
        {
            await window.NewDesignAsync();
        }
    }

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window)
        {
            await window.OpenDesignAsync();
        }
    }

    private async void Library_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window)
        {
            await new LibraryDialog().ShowDialog(window);
        }
    }

    private async void ChooseJulia_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window)
        {
            await window.ChooseJuliaAsync();
        }
    }

    private async void Recent_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window && (sender as Control)?.DataContext is RecentDesign design)
        {
            await window.OpenDesignAsync(design.Path);
        }
    }

    private void ForgetRecent_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && (sender as Control)?.DataContext is RecentDesign design)
        {
            viewModel.ForgetRecent(design);
        }
    }
}
