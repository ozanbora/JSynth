using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace JuliaSynth.Views.Details;

public partial class WatchNode : UserControl
{
    public WatchNode()
    {
        InitializeComponent();
    }

    private async void BrowseReplay_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Model.WatchNode model || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Replay the recorded files of",
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is string path)
        {
            model.ReplayFolder = path;
        }
    }
}
