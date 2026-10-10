using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace JSynth.Views.Details;

public partial class ArrayFile : UserControl
{
    private static readonly IReadOnlyList<FilePickerFileType> FileTypes = [
        new("Arrays and images (CSV, TSV, Matrix Market, JLD2, PNG)") { Patterns = ["*.csv", "*.tsv", "*.tab", "*.mtx", "*.jld2", "*.png"] },
        new("All files") { Patterns = ["*"] },
    ];

    public ArrayFile()
    {
        InitializeComponent();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Model.ArrayFile model || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load an array from",
            FileTypeFilter = FileTypes,
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
        {
            model.Path = path;
        }
    }
}
