using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JSynth.Views;

/// <summary>
/// The node library's entries, to rename or delete; and the name asked for when saving to it.
/// </summary>
public sealed class LibraryDialog : Window
{
    private readonly ListBox _entries = new() { MinHeight = 220, MaxHeight = 420 };
    private readonly TextBlock _empty = new()
    {
        Text = "Nothing saved yet. Select nodes or a group, right-click and choose Save to library….",
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.7,
    };

    public LibraryDialog()
    {
        Title = "Node library";
        Icon = AppIcon.Window;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _entries.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(LibraryEntry.Name));

        Button rename = new() { Content = "Rename…", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button delete = new() { Content = "Delete", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button folder = new() { Content = "Open folder", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button close = new() { Content = "Close", MinWidth = 88, IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        rename.Click += async (_, _) => await RenameAsync();
        delete.Click += async (_, _) => await DeleteAsync();
        folder.Click += async (_, _) => await OpenFolderAsync();
        close.Click += (_, _) => Close();
        _entries.SelectionChanged += (_, _) => rename.IsEnabled = delete.IsEnabled = _entries.SelectedItem is not null;

        Grid list = new() { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 12 };
        list.Children.Add(new Panel { Children = { _entries, _empty } });
        StackPanel actions = new() { Spacing = 6, MinWidth = 110 };
        actions.Children.AddRange([rename, delete, folder]);
        Grid.SetColumn(actions, 1);
        list.Children.Add(actions);

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Nodes and groups saved to the library; Add node › Library adds them in any design.", TextWrapping = TextWrapping.Wrap },
                list,
                close,
            },
        };

        Fill();
    }

    private void Fill(string? selected = null)
    {
        LibraryEntry[] entries = [.. MainViewModel.LibraryEntries()];
        _entries.ItemsSource = entries;
        _entries.SelectedItem = entries.FirstOrDefault(e => e.Name == selected) ?? entries.FirstOrDefault();
        (_entries.IsVisible, _empty.IsVisible) = (entries.Length > 0, entries.Length == 0);
    }

    private async Task RenameAsync()
    {
        if (_entries.SelectedItem is LibraryEntry entry && await AskNameAsync(this, "Rename", entry.Name, entry) is string name)
        {
            await Try(() => MainViewModel.RenameInLibrary(entry, name));
            Fill(MainViewModel.LibraryName(name));
        }
    }

    private async Task DeleteAsync()
    {
        if (_entries.SelectedItem is LibraryEntry entry
            && await MessageDialog.ShowAsync(this, "Delete from the library", $"Delete {entry.Name} from the library? Designs it was added to keep their copies.", "Delete", "Cancel") == "Delete")
        {
            await Try(() => MainViewModel.DeleteFromLibrary(entry));
            Fill();
        }
    }

    private async Task OpenFolderAsync()
    {
        _ = Directory.CreateDirectory(MainViewModel.LibraryFolder);
        if (Launcher is { } launcher)
        {
            _ = await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(MainViewModel.LibraryFolder));
        }
    }

    private async Task Try(Action change)
    {
        try
        {
            change();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _ = await MessageDialog.ShowAsync(this, "Can't change the library", error.Message, "OK");
        }
    }

    /// <summary>
    /// Asks for an entry's name, starting from <paramref name="suggested"/>, and whether to replace another entry of
    /// that name; null when cancelled. <paramref name="renaming"/> is the entry being renamed, if any.
    /// </summary>
    public static async Task<string?> AskNameAsync(Window owner, string title, string suggested, LibraryEntry? renaming = null)
    {
        string text = suggested;
        while (true)
        {
            (string? button, text) = await MessageDialog.AskTextAsync(owner, title, "A name for it in Add node › Library:", text, "Save", "Cancel");
            string name = MainViewModel.LibraryName(text);
            if (button != "Save" || name.Length == 0)
            {
                return null;
            }

            bool same = renaming is not null && string.Equals(name, renaming.Name, StringComparison.OrdinalIgnoreCase);
            if (same || !MainViewModel.InLibrary(name)
                || await MessageDialog.ShowAsync(owner, "Replace?", $"The library has {name} already. Replace it?", "Replace", "Cancel") == "Replace")
            {
                return name;
            }
        }
    }
}
