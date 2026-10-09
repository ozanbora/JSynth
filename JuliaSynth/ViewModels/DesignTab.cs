using Avalonia;
using JuliaSynth.Model;
using ReactiveUI;
using System.Collections.Generic;
using System.IO;

namespace JuliaSynth.ViewModels;

/// <summary>
/// A design open in a tab. The open tab's design is the one on the canvas (<see cref="MainViewModel.Nodes"/> and
/// its wires, history and file); the others keep theirs here until they are open again.
/// </summary>
public sealed class DesignTab : ReactiveObject
{
    private string? _filePath;
    private string _untitledName = "Untitled";
    private bool _isDirty;
    private bool _isActive;

    /// <summary>
    /// Its file, or null for a design never saved.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(ToolTip));
        }
    }

    /// <summary>
    /// What it is called while never saved: Untitled, or the example's name.
    /// </summary>
    public string UntitledName
    {
        get => _untitledName;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _untitledName, value);
            this.RaisePropertyChanged(nameof(Name));
        }
    }

    public string Name => FilePath is null ? UntitledName : Path.GetFileNameWithoutExtension(FilePath);

    public string ToolTip => FilePath ?? $"{UntitledName}: not saved to a file yet";

    /// <summary>
    /// Changed since last saved.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>
    /// Its design is the one on the canvas.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>
    /// Where it is kept while never saved, to open again at the next start (each tab its own).
    /// </summary>
    internal string UntitledFile { get; set; } = MainViewModel.UntitledFile;

    /// <summary>
    /// The tab JuliaSynth started with, nothing opened in it yet: an example or a design opened goes in it rather
    /// than in a new tab.
    /// </summary>
    internal bool IsFresh { get; set; }

    /// <summary>
    /// Its changes were let go as JuliaSynth closed: it isn't kept for the next start.
    /// </summary>
    internal bool IsDiscarded { get; set; }

    // While another tab is open: its design (null until first opened, for a tab restored from the last session), its
    // undo steps, and where it was scrolled and zoomed.
    internal List<Node>? Nodes { get; set; }
    internal List<Connection>? Connections { get; set; }
    internal UndoHistory.Steps? History { get; set; }
    internal Vector Offset { get; set; }
    internal double Zoom { get; set; } = 1.0;

    /// <summary>
    /// Has work that isn't in a file: changes since saved, or, never saved, any edit at all.
    /// </summary>
    internal bool HasUnsavedWork(int nodeCount, bool canUndo, bool isDirty)
    {
        return FilePath is null ? nodeCount > 0 && canUndo : isDirty;
    }
}
