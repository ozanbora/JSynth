using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JSynth.ViewModels;
using System;
using System.Threading.Tasks;

namespace JSynth.Views;

/// <summary>
/// What JSynth shows before a design: whether Julia is ready (or how to install it), the examples,
/// and ways to start. Its buttons do what the window's menus do.
/// </summary>
public partial class StartPage : UserControl
{
    public StartPage()
    {
        InitializeComponent();
    }

    private MainWindow? Window => TopLevel.GetTopLevel(this) as MainWindow;

    private async void Example_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Example example } && Window is MainWindow window)
        {
            await window.OpenExampleAsync(example);
        }
    }

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

    private async void ChooseJulia_Click(object? sender, RoutedEventArgs e)
    {
        if (Window is MainWindow window)
        {
            await window.ChooseJuliaAsync();
        }
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(MainViewModel.JuliaInstallCommand);
            CopyButton.Content = "Copied";
            await Task.Delay(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.Post(() => CopyButton.Content = "Copy");
        }
    }

    private async void InstallPage_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = await launcher.LaunchUriAsync(new Uri(MainViewModel.JuliaInstallPage));
        }
    }
}
