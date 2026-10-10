using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using JSynth.ViewModels;
using JSynth.Views;

namespace JSynth;

public partial class App : Application
{
    /// <summary>
    /// The design this window opens alone (a tab opened in a new window): its file, or where it was kept while never
    /// saved and its name. Nulls for the usual start.
    /// </summary>
    public static (string? File, string? Untitled, string? Name) StartDesign { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel.ApplyTheme(AppSettings.Load().Theme);
            MainViewModel viewModel = new();
            viewModel.StartUp(StartDesign.File, StartDesign.Untitled, StartDesign.Name);
            desktop.Exit += (_, _) =>
            {
                viewModel.RememberOpenDesign();
                viewModel.EndJulia();
                MainViewModel.DeletePlotFiles();
            };

            // Saving is explicit now; the window asks about unsaved changes when it closes.
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }


        base.OnFrameworkInitializationCompleted();
    }


}
