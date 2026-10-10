using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace JSynth.Views.Details;

public partial class SaveFile : UserControl
{
    private static readonly IReadOnlyList<FilePickerFileType> FileTypes = [
        new("CSV") { Patterns = ["*.csv"] },
        new("TSV") { Patterns = ["*.tsv"] },
        new("Matrix Market") { Patterns = ["*.mtx"] },
        new("JLD2") { Patterns = ["*.jld2"] },
    ];

    public SaveFile()
    {
        InitializeComponent();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Model.SaveFile model || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the value to",
            FileTypeChoices = FileTypes,
            DefaultExtension = ".csv",
            SuggestedFileName = model.Path.Length == 0 ? "result.csv" : System.IO.Path.GetFileName(model.Path),
            ShowOverwritePrompt = false,
        });

        if (file?.TryGetLocalPath() is string path)
        {
            model.Path = path;
        }
    }
}
