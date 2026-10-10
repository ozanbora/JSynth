using Avalonia.Collections;
using JSynth.Model;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JSynth.ViewModels;

/// <summary>
/// Where a tab's design is, for the next start: its file, or where it is kept while never saved.
/// </summary>
public sealed class SessionTab
{
    public string Path { get; set; } = string.Empty;

    public bool Untitled { get; set; }

    public string Name { get; set; } = "Untitled";
}

/// <summary>
/// Tabs: several designs open at once. The open tab's design is the canvas's (<see cref="Nodes"/>, its wires, the
/// history, the file); switching tabs puts it away in its tab and brings the other's out, undo steps and all.
/// </summary>
public partial class MainViewModel
{
    private DesignTab _activeTab = null!;

    public AvaloniaList<DesignTab> Tabs { get; } = [];

    public DesignTab ActiveTab => _activeTab;

    /// <summary>
    /// Whether this window keeps its tabs for the next start: not one opened from another's tab (see
    /// <see cref="StartUp(string?, string?, string?)"/>).
    /// </summary>
    internal bool RemembersSession { get; private set; } = true;

    private void CreateTabs()
    {
        _activeTab = new DesignTab { IsActive = true, IsFresh = true };
        Tabs.Add(_activeTab);
        History.Changed += (_, _) => _activeTab.IsDirty = IsDirty;
    }

    /// <summary>
    /// The tab with <paramref name="path"/> open, if any.
    /// </summary>
    public DesignTab? TabOf(string path)
    {
        string full = Path.GetFullPath(path);
        return Tabs.FirstOrDefault(t => t.FilePath is string p && string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Puts the open design away in its tab and shows <paramref name="tab"/>'s. A run stops: it was the other design's.
    /// </summary>
    public void ActivateTab(DesignTab tab)
    {
        if (tab == _activeTab || !Tabs.Contains(tab))
        {
            return;
        }

        PutAwayActiveTab();
        ShowTab(tab);
    }

    /// <summary>
    /// A new tab, with an empty design, open.
    /// </summary>
    public DesignTab NewTab()
    {
        PutAwayActiveTab();
        DesignTab tab = new() { UntitledFile = FreeUntitledFile(), Nodes = [], Connections = [] };
        Tabs.Insert(Tabs.IndexOf(_activeTab) + 1, tab);
        ShowTab(tab);
        return tab;
    }

    /// <summary>
    /// Before a design is opened or started: the open tab when nothing is in it yet (or it is the one JSynth
    /// started with, untouched), else a new tab.
    /// </summary>
    public void UseTabForOpening()
    {
        bool reusable = FilePath is null && !History.CanUndo && (Nodes.Count == 0 || _activeTab.IsFresh);
        if (!reusable)
        {
            _ = NewTab();
        }

        _activeTab.IsFresh = false;
    }

    /// <summary>
    /// Closes <paramref name="tab"/>, its changes as they are (ask first: see <see cref="TabHasUnsavedWork"/>). The last
    /// tab closed leaves an empty one, with the start page.
    /// </summary>
    public void CloseTab(DesignTab tab)
    {
        int index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        if (tab != _activeTab)
        {
            _ = Tabs.Remove(tab);
            return;
        }

        if (IsRunning)
        {
            StopRun();
        }

        if (Tabs.Count == 1)
        {
            NewDesign();
            tab.UntitledFile = FreeUntitledFile(tab);
            IsStartPageOpen = true;
            return;
        }

        DesignTab next = Tabs[index == Tabs.Count - 1 ? index - 1 : index + 1];
        _ = Tabs.Remove(tab);
        ShowTab(next);
    }

    /// <summary>
    /// Whether closing <paramref name="tab"/> would lose work: changes not saved to its file (autosaving saves them
    /// first), or a design never saved that was worked on.
    /// </summary>
    public bool TabHasUnsavedWork(DesignTab tab)
    {
        if (tab == _activeTab)
        {
            _ = AutoSaveNow();
            return tab.HasUnsavedWork(Nodes.Count, History.CanUndo, IsDirty);
        }

        if (tab.FilePath is not null && tab.IsDirty && AutoSave)
        {
            _ = SaveAwayTab(tab);
        }

        return tab.HasUnsavedWork(tab.Nodes?.Count ?? 0, tab.History?.CanUndo ?? false, tab.IsDirty);
    }

    /// <summary>
    /// As JSynth closes: the tabs whose changes aren't saved, autosaving what it can first. A design never saved
    /// is kept to open again rather than asked about, unless autosave is off.
    /// </summary>
    public List<DesignTab> TabsToAskAboutOnClosing()
    {
        List<DesignTab> unsaved = [];
        foreach (DesignTab tab in Tabs)
        {
            bool saved = tab == _activeTab
                ? AutoSaveNow()
                : !tab.IsDirty || (AutoSave && (tab.FilePath is null || SaveAwayTab(tab)));
            if (!saved)
            {
                unsaved.Add(tab);
            }
        }

        return unsaved;
    }

    /// <summary>
    /// The open tab's changes are to be lost: it isn't kept for the next start.
    /// </summary>
    public void DiscardActiveTab()
    {
        _activeTab.IsDiscarded = true;
    }

    /// <summary>
    /// The open tab's design kept in its file for another window to open: its file saved, or, never saved, kept as
    /// such. The command line that opens it there, or null when it couldn't be written.
    /// </summary>
    public string[]? HandOverActiveTab()
    {
        try
        {
            if (FilePath is string path)
            {
                if (IsDirty)
                {
                    SaveDesign(path);
                }

                return ["--open", path];
            }

            if (Nodes.Count == 0)
            {
                return [];
            }

            History.Commit();
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_activeTab.UntitledFile)!);
            File.WriteAllText(_activeTab.UntitledFile, CaptureDesign());
            return ["--untitled", _activeTab.UntitledFile, "--name", _activeTab.UntitledName];
        }
        catch (Exception e) when (IsFileError(e))
        {
            Log($"Couldn't keep {_activeTab.Name} for the new window: {e.Message}", OutputKind.Error);
            return null;
        }
    }

    // The open design into its tab, with its history and view.
    private void PutAwayActiveTab()
    {
        if (IsRunning)
        {
            StopRun();
        }

        DesignTab tab = _activeTab;
        tab.History = History.Save();
        tab.IsDirty = tab.History.IsDirty;
        tab.Nodes = [.. Nodes];
        tab.Connections = [.. Connections];
        tab.Offset = ScrollViewerOffset;
        tab.Zoom = SelectedZoomLevel;
        tab.IsActive = false;
    }

    // A tab's design onto the canvas: as it was put away, or, from the last session, read from its file.
    private void ShowTab(DesignTab tab)
    {
        _activeTab = tab;
        tab.IsActive = true;
        Inspected = null;
        IsStartPageOpen = false;

        if (tab.Nodes is null)
        {
            string? path = tab.FilePath ?? (File.Exists(tab.UntitledFile) ? tab.UntitledFile : null);
            try
            {
                if (path is null)
                {
                    throw new FileNotFoundException("The design isn't there any more.", tab.UntitledFile);
                }

                ReadDesign(path);
            }
            catch (Exception e) when (IsFileError(e))
            {
                Load(new DesignDocument());
                History.Reset();
                Log($"Couldn't open {tab.Name}: {e.Message}", OutputKind.Error);
            }
        }
        else
        {
            Load(new DesignDocument { Nodes = [.. tab.Nodes], Connections = [.. tab.Connections ?? []] });
            if (tab.History is UndoHistory.Steps steps)
            {
                History.Restore(steps);
            }
            else
            {
                History.Reset();
            }
        }

        (tab.Nodes, tab.Connections, tab.History) = (null, null, null);
        FilePath = tab.FilePath;
        SelectedZoomLevel = tab.Zoom;
        ScrollViewerOffset = tab.Offset;
        tab.IsDirty = IsDirty;
        this.RaisePropertyChanged(nameof(ActiveTab));
        this.RaisePropertyChanged(nameof(IsDirty));
        this.RaisePropertyChanged(nameof(Title));
        DesignOpened();
    }

    // A tab put away, saved to its file as it is; its history now counts that as saved. False when it can't be.
    private bool SaveAwayTab(DesignTab tab)
    {
        if (tab.FilePath is not string path || tab.Nodes is null || tab.History is not UndoHistory.Steps steps)
        {
            return !tab.IsDirty;
        }

        try
        {
            File.WriteAllText(path, Capture(tab.Nodes, tab.Connections ?? []));
            tab.History = steps with { Saved = steps.States[steps.Index] };
            tab.IsDirty = false;
            return true;
        }
        catch (Exception e) when (IsFileError(e))
        {
            Log($"Couldn't autosave to {path}: {e.Message}", OutputKind.Error);
            return false;
        }
    }

    private static string Capture(IEnumerable<Node> nodes, IEnumerable<Connection> connections)
    {
        return JsonConvert.SerializeObject(new DesignDocument { Nodes = [.. nodes], Connections = [.. connections] }, settings);
    }

    /// <summary>
    /// Where a new tab's design is kept while never saved: untitled, untitled-2, … , one no other tab uses.
    /// </summary>
    private string FreeUntitledFile(DesignTab? except = null)
    {
        HashSet<string> taken = [.. Tabs.Where(t => t != except).Select(t => t.UntitledFile)];
        return Enumerable.Range(1, int.MaxValue - 1)
            .Select(i => i == 1 ? UntitledFile : Path.Combine(Path.GetDirectoryName(UntitledFile)!, $"untitled-{i}{FileExtension}"))
            .First(p => !taken.Contains(p));
    }

    /// <summary>
    /// As JSynth closes: the tabs put away that were never saved are kept to open again, and every tab is
    /// remembered for the next start.
    /// </summary>
    private void RememberTabs(AppSettings settings)
    {
        foreach (DesignTab tab in Tabs.Where(t => t != _activeTab && t.FilePath is null && t.Nodes is { Count: > 0 } && !t.IsDiscarded))
        {
            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(tab.UntitledFile)!);
                File.WriteAllText(tab.UntitledFile, Capture(tab.Nodes!, tab.Connections ?? []));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                tab.IsDiscarded = true;
            }
        }

        settings.OpenTabs = [.. Tabs
            .Where(t => t == _activeTab || t.FilePath is not null || (!t.IsDiscarded && File.Exists(t.UntitledFile)))
            .Select(t => t.FilePath is string path
                ? new SessionTab { Path = path, Name = t.Name }
                : new SessionTab { Path = t.UntitledFile, Untitled = true, Name = t.UntitledName })];
        settings.ActiveTab = Tabs.Where(t => t == _activeTab || t.FilePath is not null || (!t.IsDiscarded && File.Exists(t.UntitledFile))).ToList().IndexOf(_activeTab);
    }

    /// <summary>
    /// At start-up, after the design open last is: the other tabs open then, each read when it is first shown.
    /// </summary>
    private void RestoreTabs(AppSettings settings)
    {
        if (settings.OpenTabs.Count < 2 || settings.ActiveTab < 0 || settings.ActiveTab >= settings.OpenTabs.Count)
        {
            return;
        }

        if (settings.OpenTabs[settings.ActiveTab] is { Untitled: true } active && _activeTab.FilePath is null)
        {
            _activeTab.UntitledName = active.Name;
        }

        // In the order they were, the open one where it was.
        List<DesignTab> others = [];
        for (int i = 0; i < settings.OpenTabs.Count; i++)
        {
            SessionTab saved = settings.OpenTabs[i];
            if (i == settings.ActiveTab || !File.Exists(saved.Path) || (!saved.Untitled && TabOf(saved.Path) is not null))
            {
                continue;
            }

            others.Add(saved.Untitled
                ? new DesignTab { UntitledFile = saved.Path, UntitledName = saved.Name }
                : new DesignTab { FilePath = Path.GetFullPath(saved.Path) });
        }

        int before = settings.OpenTabs.Take(settings.ActiveTab).Count(s => File.Exists(s.Path));
        Tabs.Clear();
        Tabs.AddRange(others);
        Tabs.Insert(Math.Min(before, Tabs.Count), _activeTab);
    }
}
