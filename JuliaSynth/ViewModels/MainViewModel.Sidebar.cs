using Avalonia.Collections;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace JuliaSynth.ViewModels;

/// <summary>
/// A design opened or saved before: its name, and where it is.
/// </summary>
public sealed record RecentDesign(string Name, string Path)
{
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}

/// <summary>
/// The sidebar: new and open, examples, the library and the panels, the designs opened lately, and Julia.
/// </summary>
public partial class MainViewModel
{
    private const int MaxRecentDesigns = 12;

    private bool? _isSidebarOpen;

    /// <summary>
    /// Whether the sidebar shows; kept in the settings, open unless closed.
    /// </summary>
    public bool IsSidebarOpen
    {
        get => _isSidebarOpen ??= AppSettings.Load().ShowSidebar;
        set
        {
            if (value == IsSidebarOpen)
            {
                return;
            }

            _isSidebarOpen = value;
            this.RaisePropertyChanged();
            AppSettings settings = AppSettings.Load();
            settings.ShowSidebar = value;
            settings.Save();
        }
    }

    public ICommand ToggleSidebar => _toggleSidebar ??= ReactiveCommand.Create(() => { IsSidebarOpen = !IsSidebarOpen; });

    private ICommand? _toggleSidebar;

    /// <summary>
    /// The designs opened or saved lately, the latest first, those still there.
    /// </summary>
    public AvaloniaList<RecentDesign> RecentDesigns { get; } = [];

    public bool HasRecentDesigns => RecentDesigns.Count > 0;

    /// <summary>
    /// Fills <see cref="RecentDesigns"/> from the settings.
    /// </summary>
    internal void RefreshRecentDesigns()
    {
        RecentDesigns.Clear();
        RecentDesigns.AddRange(AppSettings.Load().RecentFiles
            .Where(File.Exists)
            .Select(path => new RecentDesign(Path.GetFileNameWithoutExtension(path), path)));
        this.RaisePropertyChanged(nameof(HasRecentDesigns));
    }

    /// <summary>
    /// <paramref name="path"/> first among the recent designs.
    /// </summary>
    private static void AddRecent(AppSettings settings, string path)
    {
        settings.RecentFiles = [path, .. settings.RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).Take(MaxRecentDesigns - 1)];
    }

    /// <summary>
    /// Takes <paramref name="design"/> off the list (it is left as it is on disk).
    /// </summary>
    public void ForgetRecent(RecentDesign design)
    {
        AppSettings settings = AppSettings.Load();
        settings.RecentFiles = [.. settings.RecentFiles.Where(p => !string.Equals(p, design.Path, StringComparison.OrdinalIgnoreCase))];
        settings.Save();
        RefreshRecentDesigns();
    }
}
