using JuliaSynth.Model;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace JuliaSynth.ViewModels;

/// <summary>
/// A node, a group or a few nodes kept in the library under a name, one file each next to the settings: Add node offers
/// them in any design, each added as a copy of its own.
/// </summary>
public sealed record LibraryEntry(string Name, string Path);

/// <summary>
/// The node library: the selection saved under a name, and added again in any design.
/// </summary>
public partial class MainViewModel
{
    public const string LibraryExtension = ".jsynthnode";

    /// <summary>
    /// Where the library's entries are kept: a folder next to the settings.
    /// </summary>
    internal static string LibraryFolder => Path.Combine(Path.GetDirectoryName(AppSettings.SettingsPath)!, "library");

    /// <summary>
    /// The selection saved to the library, under a name asked for.
    /// </summary>
    public ICommand SaveToLibrary { get; private set; } = null!;

    /// <summary>
    /// Asks for a name to save the selection under, suggesting one; null when cancelled. Set by the window.
    /// </summary>
    public Func<string, Task<string?>>? AskLibraryName { get; set; }

    private void CreateLibraryCommands()
    {
        IObservable<bool> notTyping = this.WhenAnyValue(x => x.IsTypingText, typing => !typing);
        SaveToLibrary = ReactiveCommand.CreateFromTask(async () =>
        {
            if (AskLibraryName is null || SuggestedLibraryName() is not string suggested || await AskLibraryName(suggested) is not string name)
            {
                return;
            }

            try
            {
                if (SaveSelectionToLibrary(name))
                {
                    Log($"Saved {name} to the library: Add node › Library adds it in any design.", OutputKind.Info);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log($"Couldn't save {name} to the library: {error.Message}", OutputKind.Error);
            }
        }, notTyping);
    }

    /// <summary>
    /// The library's entries, by name.
    /// </summary>
    public static IReadOnlyList<LibraryEntry> LibraryEntries()
    {
        return Directory.Exists(LibraryFolder)
            ? [.. Directory.EnumerateFiles(LibraryFolder, "*" + LibraryExtension)
                .Select(path => new LibraryEntry(Path.GetFileNameWithoutExtension(path), path))
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)]
            : [];
    }

    /// <summary>
    /// <paramref name="name"/> as an entry's name: no characters a file name can't have, no spaces around it.
    /// </summary>
    public static string LibraryName(string name)
    {
        char[] invalid = [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];
        return new string([.. name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]).Trim().TrimEnd('.');
    }

    /// <summary>
    /// Whether the library has an entry named <paramref name="name"/>.
    /// </summary>
    public static bool InLibrary(string name) => File.Exists(LibraryPath(name));

    private static string LibraryPath(string name) => Path.Combine(LibraryFolder, LibraryName(name) + LibraryExtension);

    /// <summary>
    /// A name for the selection in the library: a group's or a function's alone, else "nodes". Null when nothing is
    /// selected.
    /// </summary>
    public static string? SuggestedLibraryName()
    {
        return Nodes.Where(n => n.IsSelected).ToList() switch
        {
            [] => null,
            [GroupNode group] => group.Name,
            [Function function] => function.Name,
            [FrameNode frame] => frame.Title,
            _ => "nodes",
        };
    }

    /// <summary>
    /// Saves the selected nodes and the wires between them as <paramref name="name"/>, replacing an entry of that name.
    /// False when nothing is selected.
    /// </summary>
    public static bool SaveSelectionToLibrary(string name)
    {
        if (CaptureSelection() is not string copy || LibraryName(name).Length == 0)
        {
            return false;
        }

        _ = Directory.CreateDirectory(LibraryFolder);
        File.WriteAllText(LibraryPath(name), copy);
        return true;
    }

    /// <summary>
    /// Adds a copy of <paramref name="entry"/>'s nodes, its top left one at the cell given; they become the selection.
    /// </summary>
    public static void AddFromLibrary(LibraryEntry entry, int row, int column)
    {
        PasteCopy(File.ReadAllText(entry.Path), 0, (row, column));
    }

    /// <summary>
    /// Names <paramref name="entry"/> <paramref name="name"/> instead, replacing an entry of that name.
    /// </summary>
    public static void RenameInLibrary(LibraryEntry entry, string name)
    {
        string path = LibraryPath(name);
        if (!string.Equals(path, entry.Path, StringComparison.Ordinal))
        {
            File.Move(entry.Path, path, overwrite: true);
        }
    }

    public static void DeleteFromLibrary(LibraryEntry entry)
    {
        File.Delete(entry.Path);
    }
}
