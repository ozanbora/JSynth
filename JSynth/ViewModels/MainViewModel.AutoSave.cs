using Avalonia.Threading;
using ReactiveUI;
using System;
using System.IO;

namespace JSynth.ViewModels;

/// <summary>
/// Autosave: a moment after each change, a design file is saved, and a design never saved is kept to
/// open again at the next start (see <see cref="RememberOpenDesign"/>), so nothing is lost on closing.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// How long after a change (once it is an undo step) the design is saved.
    /// </summary>
    internal static TimeSpan AutoSaveDelay { get; set; } = TimeSpan.FromSeconds(1);

    // The design is the app's, and History everyone's: only the view model started last saves it.
    private static MainViewModel? _autoSaver;

    private DispatcherTimer? _autoSaveTimer;
    private bool _autoSave;
    private string? _autoSaveFailure;

    /// <summary>
    /// Whether the design saves itself; kept in the settings, on unless turned off.
    /// </summary>
    public bool AutoSave
    {
        get => _autoSave;
        set
        {
            if (value == _autoSave)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _autoSave, value);
            AppSettings settings = AppSettings.Load();
            settings.AutoSave = value;
            settings.Save();
            if (value)
            {
                ScheduleAutoSave();
            }
        }
    }

    /// <summary>
    /// At start-up: this view model saves the design from now on, if the settings say so.
    /// </summary>
    private void StartAutoSave()
    {
        _autoSaver = this;
        _autoSave = AppSettings.Load().AutoSave;
        this.RaisePropertyChanged(nameof(AutoSave));
        if (_autoSaveTimer is null)
        {
            _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveDelay };
            _autoSaveTimer.Tick += (_, _) =>
            {
                _autoSaveTimer.Stop();
                _ = AutoSaveNow();
            };
            History.Changed += (_, _) => ScheduleAutoSave();
        }
    }

    /// <summary>
    /// No view model saves the design any more: for tests, whose settings and files go away.
    /// </summary>
    internal static void StopAutoSave()
    {
        _autoSaver?._autoSaveTimer?.Stop();
        _autoSaver = null;
    }

    private void ScheduleAutoSave()
    {
        if (_autoSaveTimer is null || !AutoSave || _autoSaver != this || !IsDirty)
        {
            return;
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Interval = AutoSaveDelay;
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// Saves the design now, if autosaving and changed: to its file, or, never saved, kept for the next
    /// start. True when nothing is left unsaved. A file that can't be written is said once, in the output.
    /// </summary>
    public bool AutoSaveNow()
    {
        _autoSaveTimer?.Stop();
        if (!AutoSave || _autoSaver != this || !IsDirty)
        {
            return !IsDirty;
        }

        string target = FilePath ?? _activeTab.UntitledFile;
        try
        {
            if (FilePath is string path)
            {
                SaveDesign(path);
            }
            else if (Nodes.Count > 0)
            {
                KeepUntitled();
            }
            else
            {
                // Empty: nothing to keep.
                return true;
            }

            _autoSaveFailure = null;
            return true;
        }
        catch (Exception e) when (IsFileError(e))
        {
            if (_autoSaveFailure != e.Message)
            {
                _autoSaveFailure = e.Message;
                Log($"Couldn't autosave to {target}: {e.Message}", OutputKind.Error);
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps the design never saved, to open again at the next start as it is.
    /// </summary>
    /// <exception cref="IOException">It can't be written.</exception>
    private void KeepUntitled()
    {
        History.Commit();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(_activeTab.UntitledFile)!);
        File.WriteAllText(_activeTab.UntitledFile, CaptureDesign());
        History.MarkSaved();
        if (!RemembersSession)
        {
            return;
        }

        AppSettings settings = AppSettings.Load();
        settings.LastFile = _activeTab.UntitledFile;
        settings.LastUntitled = true;
        RememberTabs(settings);
        settings.Save();
    }
}
