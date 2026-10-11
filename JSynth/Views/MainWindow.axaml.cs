using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JSynth.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType DesignFileType = new("JSynth design")
    {
        Patterns = [$"*{MainViewModel.FileExtension}"],
    };

    // Set once unsaved changes have been dealt with, so closing doesn't ask again.
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        foreach (Example example in Examples.All)
        {
            MenuItem item = new() { Header = example.Name };
            ToolTip.SetTip(item, example.Description);
            item.Click += async (_, _) => await OpenExampleAsync(example);
            ExamplesMenu.Items.Add(item);
        }

        // The title bar is the app's own: the one Avalonia draws keeps only its minimize, maximize and close.
        Opened += (_, _) => Dispatcher.UIThread.Post(HideDrawnTitle, DispatcherPriority.Background);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                Dispatcher.UIThread.Post(HideDrawnTitle, DispatcherPriority.Background);
            }
        };

        // Ctrl+Tab goes to the next tab before keyboard navigation takes Tab for itself.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                NextTab(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        // The window's shortcuts edit the design; while a text field has the keyboard, its keys are the text's.
        AddHandler(GotFocusEvent, (_, e) => SetTyping(e.Source), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, _) => Dispatcher.UIThread.Post(() => SetTyping(FocusManager?.GetFocusedElement())), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // The window's title and full-screen button, drawn by Avalonia in a layer above the window that its styles don't
    // reach.
    private void HideDrawnTitle()
    {
        Avalonia.Visual root = this;
        while (root.GetVisualParent() is Avalonia.Visual parent)
        {
            root = parent;
        }

        foreach (Control part in root.GetVisualDescendants().OfType<Control>().Where(c => c.Name is "PART_TitleTextPanel" or "PART_FullScreenButton"))
        {
            part.IsVisible = false;
        }
    }

    private void SetTyping(object? focused)
    {
        if (ViewModel is MainViewModel viewModel)
        {
            viewModel.IsTypingText = focused is TextBox or AvaloniaEdit.Editing.TextArea;
        }
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel is MainViewModel viewModel)
        {
            viewModel.AskToInstall = AskToInstallAsync;
            viewModel.AskLibraryName = suggested => LibraryDialog.AskNameAsync(this, "Save to library", suggested);
            viewModel.SetClipboardText = async text =>
            {
                if (Clipboard is not null)
                {
                    await Clipboard.SetTextAsync(text);
                }
            };
        }
    }

    private async Task<PackageChoice> AskToInstallAsync(IReadOnlyList<string> packages, bool offerRunAnyway)
    {
        bool one = packages.Count == 1;
        string names = one ? packages[0] : $"{string.Join(", ", packages.SkipLast(1))} and {packages[^1]}";
        string message = $"The design needs the Julia package{(one ? string.Empty : "s")} {names}, which {(one ? "isn't" : "aren't")} installed. "
            + $"Install {(one ? "it" : "them")} now with Julia's package manager? Packages are downloaded from the internet; "
            + "the first time, this can take a few minutes.";

        string[] buttons = offerRunAnyway ? ["Install", "Run without", "Cancel"] : ["Install", "Cancel"];
        return await MessageDialog.ShowAsync(this, "Missing Julia packages", message, buttons) switch
        {
            "Install" => PackageChoice.Install,
            "Run without" => PackageChoice.RunAnyway,
            _ => PackageChoice.Cancel,
        };
    }

    private async void New_Click(object? sender, RoutedEventArgs e)
    {
        await NewDesignAsync();
    }

    /// <summary>
    /// Opens <paramref name="example"/>, in a tab of its own (unless the open one has nothing in it yet).
    /// </summary>
    internal async Task OpenExampleAsync(Example example)
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        // Data that isn't bundled is downloaded first, if the user agrees, to where they like.
        if (example.Data is ExampleData data && data.DownloadedFolder is null)
        {
            const string ChangeFolder = "Change folder…";
            string folder = data.Folder;
            ExampleDataSet? set = null;
            while (set is null)
            {
                string sets = string.Join("\n", data.Sets.Select(s => $"{s.Name}: {s.Description}"));
                string? answer = await MessageDialog.ShowAsync(this, $"Download {data.Name}?",
                    $"{example.Name} reads {data.Name}, which isn't on this computer yet.\n\n{data.About}\n\n{sets}\n\n{data.Credit}\n\n"
                    + $"Download them to {folder}?",
                    [.. data.Sets.Select(s => s.Label), ChangeFolder, "Cancel"]);
                if (answer == ChangeFolder)
                {
                    IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = $"Download {data.Name} to",
                    });
                    folder = folders.FirstOrDefault()?.TryGetLocalPath() ?? folder;
                    continue;
                }

                set = data.Sets.FirstOrDefault(s => s.Label == answer);
                if (set is null)
                {
                    return;
                }
            }

            if (!await viewModel.DownloadExampleDataAsync(data, set, folder))
            {
                return;
            }
        }

        viewModel.UseTabForOpening();
        viewModel.OpenExample(example);
    }

    /// <summary>
    /// Starts an empty design, in a new tab (unless the open one has nothing in it yet).
    /// </summary>
    internal Task NewDesignAsync()
    {
        if (ViewModel is MainViewModel viewModel)
        {
            viewModel.UseTabForOpening();
            viewModel.NewDesign();
        }

        return Task.CompletedTask;
    }

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        await OpenDesignAsync();
    }

    /// <summary>
    /// Asks for a design file and opens it in a tab.
    /// </summary>
    internal async Task OpenDesignAsync()
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open design",
            FileTypeFilter = [DesignFileType],
            SuggestedStartLocation = await StartFolderAsync(viewModel),
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is string path)
        {
            await OpenAsync(viewModel, path);
        }
    }

    /// <summary>
    /// Opens the design in <paramref name="path"/> (one of the recent ones) in a tab.
    /// </summary>
    internal async Task OpenDesignAsync(string path)
    {
        if (ViewModel is MainViewModel viewModel)
        {
            await OpenAsync(viewModel, path);
        }
    }

    // Its tab if it is open already, else a tab of its own (unless the open one has nothing in it yet).
    private async Task OpenAsync(MainViewModel viewModel, string path)
    {
        if (viewModel.TabOf(path) is DesignTab open)
        {
            viewModel.ActivateTab(open);
            return;
        }

        try
        {
            viewModel.UseTabForOpening();
            viewModel.OpenDesign(path);
        }
        catch (Exception error) when (MainViewModel.IsFileError(error))
        {
            _ = await MessageDialog.ShowAsync(this, "Can't open the design", $"{Path.GetFileName(path)} couldn't be opened.\n\n{error.Message}", "OK");
        }
    }

    // The File menu's shortcuts: its items are in the ☰ menu, whose own hot keys only work while it is open.
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        Func<Task>? action = e.Key switch
        {
            Key.N when !shift => NewDesignAsync,
            Key.O when !shift => OpenDesignAsync,
            Key.S when shift => () => SaveAsAsync(),
            Key.S => () => SaveAsync(),
            Key.B when !shift => () => { ViewModel?.ToggleSidebar.Execute(null); return Task.CompletedTask; },
            Key.T when !shift => NewDesignAsync,
            Key.W when !shift && ViewModel is MainViewModel open => () => CloseTabAsync(open.ActiveTab),
            _ => null,
        };

        if (action is not null)
        {
            e.Handled = true;
            await action();
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

        // Autosaving leaves nothing to ask about, unless it can't save.
        if (_closeConfirmed || ViewModel is not MainViewModel viewModel || viewModel.TabsToAskAboutOnClosing() is not { Count: > 0 } unsaved)
        {
            return;
        }

        // Closing can't wait for the questions, so stop it, ask about each tab, and close again if allowed.
        e.Cancel = true;
        foreach (DesignTab tab in unsaved)
        {
            viewModel.ActivateTab(tab);
            if (!await ConfirmDiscardAsync())
            {
                return;
            }

            if (viewModel.IsDirty)
            {
                viewModel.DiscardActiveTab();
            }
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>
    /// Closes <paramref name="tab"/>, asking first about work that isn't saved.
    /// </summary>
    internal async Task CloseTabAsync(DesignTab tab)
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.TabHasUnsavedWork(tab))
        {
            viewModel.ActivateTab(tab);
            string? answer = await MessageDialog.ShowAsync(this, "Unsaved changes", $"Save changes to {tab.Name}?", "Save", "Don't save", "Cancel");
            if (answer is not ("Save" or "Don't save") || (answer == "Save" && !await SaveAsync()))
            {
                return;
            }
        }

        viewModel.CloseTab(tab);
    }

    /// <summary>
    /// Opens <paramref name="tab"/>'s design in a new window of its own, and closes the tab here: its file saved, or,
    /// never saved, kept for that window to open.
    /// </summary>
    internal async Task OpenInNewWindowAsync(DesignTab tab)
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.ActivateTab(tab);
        if (viewModel.HandOverActiveTab() is not string[] arguments)
        {
            _ = await MessageDialog.ShowAsync(this, "Can't open a new window", $"{tab.Name} couldn't be kept for the new window: see the output.", "OK");
            return;
        }

        try
        {
            StartNewWindow(arguments);
            viewModel.CloseTab(tab);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _ = await MessageDialog.ShowAsync(this, "Can't open a new window", error.Message, "OK");
        }
    }

    // Another JSynth, with these arguments: run as this one is (the program, or dotnet with the program's dll).
    private static void StartNewWindow(string[] arguments)
    {
        string program = Environment.ProcessPath ?? throw new InvalidOperationException("JSynth's program isn't known.");
        System.Diagnostics.ProcessStartInfo start = new(program) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(program).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && System.Reflection.Assembly.GetEntryAssembly()?.Location is string dll)
        {
            start.ArgumentList.Add(dll);
        }

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        _ = System.Diagnostics.Process.Start(start);
    }

    // A tab clicked opens it; middle-clicked, closes it.
    private async void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not MainViewModel viewModel || (sender as Control)?.DataContext is not DesignTab tab)
        {
            return;
        }

        PointerPointProperties pointer = e.GetCurrentPoint(sender as Control).Properties;
        if (pointer.IsMiddleButtonPressed)
        {
            e.Handled = true;
            await CloseTabAsync(tab);
        }
        else if (pointer.IsLeftButtonPressed)
        {
            viewModel.ActivateTab(tab);
        }
    }

    private async void TabClose_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is DesignTab tab)
        {
            await CloseTabAsync(tab);
        }
    }

    private async void TabCloseOthers_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is MainViewModel viewModel && (sender as Control)?.DataContext is DesignTab keep)
        {
            foreach (DesignTab tab in viewModel.Tabs.Where(t => t != keep).ToList())
            {
                await CloseTabAsync(tab);
            }

            viewModel.ActivateTab(keep);
        }
    }

    private async void TabNewWindow_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is DesignTab tab)
        {
            await OpenInNewWindowAsync(tab);
        }
    }

    private async void CloseActiveTab_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is MainViewModel viewModel)
        {
            await CloseTabAsync(viewModel.ActiveTab);
        }
    }

    private async void ActiveTabNewWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is MainViewModel viewModel)
        {
            await OpenInNewWindowAsync(viewModel.ActiveTab);
        }
    }

    // Ctrl+Tab and Ctrl+Shift+Tab: the next tab, or the one before, round.
    private void NextTab(int step)
    {
        if (ViewModel is MainViewModel viewModel && viewModel.Tabs.Count > 1)
        {
            int index = viewModel.Tabs.IndexOf(viewModel.ActiveTab);
            viewModel.ActivateTab(viewModel.Tabs[(index + step + viewModel.Tabs.Count) % viewModel.Tabs.Count]);
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

    private async void ChooseJulia_Click(object? sender, RoutedEventArgs e)
    {
        await ChooseJuliaAsync();
    }

    /// <summary>
    /// Asks for the Julia program and uses it from now on.
    /// </summary>
    internal async Task ChooseJuliaAsync()
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the Julia program (julia.exe, in the bin folder of Julia)",
            FileTypeFilter = OperatingSystem.IsWindows()
                ? [new FilePickerFileType("Julia") { Patterns = ["julia.exe"] }, FilePickerFileTypes.All]
                : [FilePickerFileTypes.All],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is string path)
        {
            _ = await viewModel.ChooseJuliaAsync(path);
        }
    }

    private async void ExportScript_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not MainViewModel viewModel)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Julia script",
            FileTypeChoices = [new FilePickerFileType("Julia script") { Patterns = ["*.jl"] }],
            DefaultExtension = ".jl",
            SuggestedFileName = (viewModel.FilePath is null ? "Untitled" : Path.GetFileNameWithoutExtension(viewModel.FilePath)) + ".jl",
            SuggestedStartLocation = await StartFolderAsync(viewModel),
            ShowOverwritePrompt = true,
        });

        if (file?.TryGetLocalPath() is not string path)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, MainViewModel.ExportScript());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _ = await MessageDialog.ShowAsync(this, "Can't export the script", $"{Path.GetFileName(path)} couldn't be written.\n\n{error.Message}", "OK");
        }
    }

    private async void ManageLibrary_Click(object? sender, RoutedEventArgs e)
    {
        await new LibraryDialog().ShowDialog(this);
    }

    private async void ExportImage_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not MainViewModel viewModel
            || this.GetVisualDescendants().OfType<Controls.DesignerGrid>().FirstOrDefault() is not Controls.DesignerGrid grid)
        {
            return;
        }

        if (grid.Picture() is not Avalonia.Media.Imaging.RenderTargetBitmap picture)
        {
            _ = await MessageDialog.ShowAsync(this, "Nothing to export", "The design has no nodes to draw.", "OK");
            return;
        }

        using (picture)
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export image",
                FileTypeChoices = [FilePickerFileTypes.ImagePng],
                DefaultExtension = ".png",
                SuggestedFileName = (viewModel.FilePath is null ? "Untitled" : Path.GetFileNameWithoutExtension(viewModel.FilePath)) + ".png",
                SuggestedStartLocation = await StartFolderAsync(viewModel),
                ShowOverwritePrompt = true,
            });

            if (file?.TryGetLocalPath() is not string path)
            {
                return;
            }

            try
            {
                picture.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _ = await MessageDialog.ShowAsync(this, "Can't export the image", $"{Path.GetFileName(path)} couldn't be written.\n\n{error.Message}", "OK");
            }
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
