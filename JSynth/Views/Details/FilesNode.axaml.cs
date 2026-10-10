using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace JSynth.Views.Details;

public partial class FilesNode : UserControl
{
    public FilesNode()
    {
        InitializeComponent();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Model.FilesNode model || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Read the files of",
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is string path)
        {
            model.Folder = path;
        }
    }
}
