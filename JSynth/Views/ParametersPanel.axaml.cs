using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JSynth.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Views;

/// <summary>
/// The parameters panel, docked on the left of the canvas.
/// </summary>
public partial class ParametersPanel : UserControl
{
    public ParametersPanel()
    {
        InitializeComponent();
    }

    private async void PickFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: PanelParameter parameter } || TopLevel.GetTopLevel(this)?.StorageProvider is not IStorageProvider storage)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Folder for {parameter.Name}",
            SuggestedStartLocation = parameter.Text.Length > 0 ? await storage.TryGetFolderFromPathAsync(parameter.Text) : null,
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is string path)
        {
            parameter.Text = path;
        }
    }
}
