using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JuliaSynth.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType DesignFileType = new("JuliaSynth design")
    {
        Patterns = [$"*{MainViewModel.FileExtension}"],
    };

    // Designs saved before design files existed (nodes.json).
    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
    };

    // Set once unsaved changes have been dealt with, so closing doesn't ask again.
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private async void New_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is MainViewModel viewModel && await ConfirmDiscardAsync())
        {
            viewModel.NewDesign();
        }
    }

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not MainViewModel viewModel || !await ConfirmDiscardAsync())
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open design",
            FileTypeFilter = [DesignFileType, JsonFileType],
            SuggestedStartLocation = await StartFolderAsync(viewModel),
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is not string path)
        {
            return;
        }

        try
        {
            viewModel.OpenDesign(path);
        }
        catch (Exception error) when (MainViewModel.IsFileError(error))
        {
            _ = await MessageDialog.ShowAsync(this, "Can't open the design", $"{Path.GetFileName(path)} couldn't be opened.\n\n{error.Message}", "OK");
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        _ = await SaveAsync();
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        _ = await SaveAsAsync();
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_closeConfirmed || ViewModel?.IsDirty != true)
        {
            return;
        }

        // Closing can't wait for the question, so stop it, ask, and close again if allowed.
        e.Cancel = true;
        if (await ConfirmDiscardAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>
    /// Asks what to do with unsaved changes. True when it's fine to go on (saved, or discarded).
    /// </summary>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (ViewModel is not MainViewModel viewModel || !viewModel.IsDirty)
        {
            return true;
        }

        string name = viewModel.FilePath is null ? "Untitled" : Path.GetFileName(viewModel.FilePath);
        string? answer = await MessageDialog.ShowAsync(this, "Unsaved changes", $"Save changes to {name}?", "Save", "Don't save", "Cancel");

        return answer switch
        {
            "Save" => await SaveAsync(),
            "Don't save" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Saves to the open file, or asks where for a new design. True when saved.
    /// </summary>
    private async Task<bool> SaveAsync()
    {
        return ViewModel?.FilePath is string path ? await SaveToAsync(path) : await SaveAsAsync();
    }

    private async Task<bool> SaveAsAsync()
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return false;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save design",
            FileTypeChoices = [DesignFileType],
            DefaultExtension = MainViewModel.FileExtension,
            SuggestedFileName = viewModel.FilePath is null
                ? $"Untitled{MainViewModel.FileExtension}"
                : Path.GetFileNameWithoutExtension(viewModel.FilePath) + MainViewModel.FileExtension,
            SuggestedStartLocation = await StartFolderAsync(viewModel),
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath() is string path && await SaveToAsync(path);
    }

    private async Task<bool> SaveToAsync(string path)
    {
        try
        {
            ViewModel?.SaveDesign(path);
            return true;
        }
        catch (Exception error) when (MainViewModel.IsFileError(error))
        {
            _ = await MessageDialog.ShowAsync(this, "Can't save the design", $"{Path.GetFileName(path)} couldn't be saved.\n\n{error.Message}", "OK");
            return false;
        }
    }

    /// <summary>
    /// The open file's folder, else Documents.
    /// </summary>
    private async Task<IStorageFolder?> StartFolderAsync(MainViewModel viewModel)
    {
        string? folder = viewModel.FilePath is null ? null : Path.GetDirectoryName(viewModel.FilePath);
        return folder is not null
            ? await StorageProvider.TryGetFolderFromPathAsync(folder)
            : await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
    }
}
