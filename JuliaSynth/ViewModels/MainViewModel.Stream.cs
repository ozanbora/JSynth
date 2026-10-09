using JuliaSynth.Model;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels;

/// <summary>
/// Running a design that watches a folder: a stream, going on until stopped, with its values shown
/// live, and recorded files replayed into the folder as a stand-in for a sensor.
/// </summary>
public partial class MainViewModel
{
    // How long Stop waits for a stream to end after the file it is at, before ending its Julia.
    private static readonly TimeSpan StreamStopWait = TimeSpan.FromSeconds(5);

    private bool _isStreaming;
    private string _streamStatus = string.Empty;
    private string? _streamSource;
    private string _streamWaitsFor = "the first file";
    private string? _stopFile;
    private CancellationTokenSource? _sources;
    private (List<FileInfo> Files, string Into, double Rate)? _pendingReplay;
    private WatchNode? _pendingFetch;

    // Values are logged the first time a node shows one in a stream, failures when they change: not every file's.
    private readonly HashSet<string> _streamLogged = [];
    private readonly Dictionary<string, string> _streamFailures = [];

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _isStreaming, value);
            this.RaisePropertyChanged(nameof(IsStreamBannerVisible));
            this.RaisePropertyChanged(nameof(StreamText));
        }
    }

    /// <summary>
    /// For the banner while streaming: where from, how far, and that edits wait for the next run.
    /// </summary>
    public string StreamText => $"Streaming {_streamSource}: {(_streamStatus.Length == 0 ? $"waiting for {_streamWaitsFor}" : _streamStatus)}. "
        + "Changes to the design apply when you stop and run again.";

    private string StreamStatus
    {
        get => _streamStatus;
        set
        {
            _streamStatus = value;
            this.RaisePropertyChanged(nameof(StreamText));
        }
    }

    /// <summary>
    /// Stop: a stream ends after the file it is at, keeping its Julia; anything else (or a stream not
    /// ending in time) ends its Julia.
    /// </summary>
    private void StopRun()
    {
        if (_runCancellation is not CancellationTokenSource cancellation)
        {
            return;
        }

        if (IsStreaming && _stopFile is string stop)
        {
            try
            {
                File.WriteAllText(stop, string.Empty);
                Log("Stopping the stream after the file it is at…", OutputKind.Info);
                _ = Task.Delay(StreamStopWait).ContinueWith(_ =>
                {
                    try
                    {
                        cancellation.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                        // It ended in time.
                    }
                }, TaskScheduler.Default);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Can't ask it to stop: end it.
            }
        }

        cancellation.Cancel();
    }

    /// <summary>
    /// A new file for a run to watch for, whose appearing ends its stream.
    /// </summary>
    private static string NewStopFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "JuliaSynth");
        _ = Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"stop-{Environment.ProcessId}-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// A run of <paramref name="watched"/>'s stream is starting: show it. Its replay, if it has one, waits
    /// for the stream to watch (see <see cref="StreamWatching"/>): files written before would count as there already.
    /// </summary>
    private void BeginStream(WatchNode watched, string stopFile)
    {
        // A folder fetched into is made now, before Julia looks at it.
        if (watched.Fetches)
        {
            try
            {
                _ = Directory.CreateDirectory(watched.Folder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log($"The folder to fetch into can't be made: {e.Message}", OutputKind.Error);
            }
        }

        StartStream($"from {watched.Folder}", "the first file", stopFile);
        _pendingReplay = watched.Replays && PrepareReplay(watched.ReplayFolder, watched.Folder, watched.Pattern) is List<FileInfo> files
            ? (files, watched.Folder, watched.ReplayRate)
            : null;
        _pendingFetch = watched.Fetches ? watched : null;
    }

    /// <summary>
    /// A run of <paramref name="clock"/>'s stream is starting: show it. A button pressed to start it has that press
    /// count as its first.
    /// </summary>
    private void BeginStream(TickNode clock, string stopFile)
    {
        StartStream(clock.Label, $"the first {clock.TickName}", stopFile);
        _streamButton = clock as ButtonNode;
        if (_streamButton is ButtonNode button && _pressWhenStarted)
        {
            Press(button);
        }

        _pressWhenStarted = false;
    }

    // The button whose presses the stream going on waits for.
    private ButtonNode? _streamButton;

    // Set when a button is pressed with nothing running: the run starting is that press.
    private bool _pressWhenStarted;

    /// <summary>
    /// Presses <paramref name="button"/>: in its stream, the nodes after it are worked out again; with nothing running,
    /// the design runs, this press its first.
    /// </summary>
    public void Press(ButtonNode button)
    {
        if (IsStreaming && _streamButton == button && _stopFile is string stop)
        {
            try
            {
                File.AppendAllText(ButtonNode.PressesFile(stop), "press\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log($"The press couldn't be passed on: {e.Message}", OutputKind.Error);
            }
        }
        else if (!IsRunning && ((System.Windows.Input.ICommand)Run).CanExecute(null))
        {
            _pressWhenStarted = true;
            ((System.Windows.Input.ICommand)Run).Execute(null);
        }
    }

    /// <summary>
    /// Shows a stream starting: where its values come from (<paramref name="source"/>, e.g. "from C:\data" or "every
    /// 2 s"), and what it waits for before the first.
    /// </summary>
    private void StartStream(string source, string waitsFor, string stopFile)
    {
        _stopFile = stopFile;
        _streamSource = source;
        _streamWaitsFor = waitsFor;
        _streamStatus = string.Empty;
        _streamLogged.Clear();
        _streamFailures.Clear();
        IsStreaming = true;
    }

    /// <summary>
    /// The stream watches its folder now: the replay, or the fetching, can start.
    /// </summary>
    private void StreamWatching()
    {
        _sources ??= new CancellationTokenSource();
        if (_pendingReplay is (List<FileInfo> files, string into, double rate))
        {
            _pendingReplay = null;
            _ = ReplayAsync(files, into, rate, _sources.Token);
        }

        if (_pendingFetch is WatchNode watched)
        {
            _pendingFetch = null;
            _ = FetchAsync(Http, watched.FetchUrl, watched.Folder, watched.FetchEvery, watched.FetchedName, _sources.Token);
        }
    }

    private void EndStream()
    {
        _pressWhenStarted = false;
        _pendingReplay = null;
        _pendingFetch = null;
        _sources?.Cancel();
        _sources = null;
        if (_stopFile is string stop)
        {
            try
            {
                File.Delete(stop);
                File.Delete(ButtonNode.PressesFile(stop));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Left for the system's temp clean-up.
            }
        }

        _stopFile = null;
        _streamButton = null;
        IsStreaming = false;
    }

    /// <summary>
    /// Whether to log a report of a stream: a node's first value, and a failure when it changes.
    /// </summary>
    private bool LogsInStream(Runtime.Report report)
    {
        if (report.Status == Runtime.ReportStatus.Ok)
        {
            _ = _streamFailures.Remove(report.Id);
            return _streamLogged.Add(report.Id);
        }

        if (_streamFailures.TryGetValue(report.Id, out string? last) && last == report.Text)
        {
            return false;
        }

        _streamFailures[report.Id] = report.Text;
        return true;
    }

    /// <summary>
    /// The files of <paramref name="from"/> matching <paramref name="pattern"/> to replay into
    /// <paramref name="into"/>, in name order; copies of them there already (the same name and size: from an
    /// earlier replay) are removed, so they come again rather than count as there before the stream. Done
    /// before the stream looks at the folder. Null, saying why, when there is nothing to replay.
    /// </summary>
    internal List<FileInfo>? PrepareReplay(string from, string into, string pattern)
    {
        try
        {
            if (Path.GetFullPath(from).TrimEnd('/', '\\').Equals(Path.GetFullPath(into).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
            {
                Log("Nothing replayed: the files to replay must be in another folder than the one watched.", OutputKind.Error);
                return null;
            }

            Regex matches = new(FilesNode.PatternRegex(pattern), RegexOptions.IgnoreCase);
            List<FileInfo> files = new DirectoryInfo(from).EnumerateFiles()
                .Where(f => matches.IsMatch(f.Name))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                Log($"Nothing replayed: no {(pattern.Length == 0 ? "*" : pattern)} file in {from}.", OutputKind.Error);
                return null;
            }

            _ = Directory.CreateDirectory(into);
            int removed = 0;
            foreach (FileInfo file in files)
            {
                FileInfo copy = new(Path.Combine(into, file.Name));
                if (copy.Exists && copy.Length == file.Length)
                {
                    copy.Delete();
                    removed++;
                }
            }

            if (removed > 0)
            {
                Log($"Removed the {removed} copies an earlier replay left in {into}, so they come again.", OutputKind.Info);
            }

            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log($"Nothing replayed: {e.Message}", OutputKind.Error);
            return null;
        }
    }

    /// <summary>
    /// Fetches <paramref name="url"/> every <paramref name="every"/> seconds and saves each answer into
    /// <paramref name="into"/> as a new file, named by <paramref name="name"/> from the time (UTC): a web service as
    /// the sensor. Each is written to a temporary name and then renamed, so the stream never reads half of one. A
    /// failed fetch is said once and tried again; a service saying it was asked too often (429) ends the fetching.
    /// </summary>
    internal async Task FetchAsync(System.Net.Http.HttpClient client, string url, string into, double every, Func<DateTime, string> name, CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            Log($"Fetching {url} every {every:0.#} s into {into}, a web service as the sensor.", OutputKind.Info);
            _ = Directory.CreateDirectory(into);
            while (true)
            {
                try
                {
                    using System.Net.Http.HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Log($"Stopped fetching: {url} says it was asked too often (429). Services limit how often they may be asked: try again later, or less often.", OutputKind.Error);
                        return;
                    }

                    _ = response.EnsureSuccessStatusCode();
                    byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    string target = Path.Combine(into, name(DateTime.UtcNow));
                    await File.WriteAllBytesAsync(target + ".part", body, cancellationToken);
                    File.Move(target + ".part", target, overwrite: true);
                    failure = null;
                }
                catch (System.Net.Http.HttpRequestException e) when (e.Message != failure)
                {
                    failure = e.Message;
                    Log($"Fetching failed, trying again every {every:0.#} s: {e.Message}", OutputKind.Error);
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Failing as before: said already.
                }

                await Task.Delay(TimeSpan.FromSeconds(every), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The stream ended.
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"The fetching stopped: {e.Message}", OutputKind.Error);
        }
    }

    /// <summary>
    /// Copies <paramref name="files"/> into <paramref name="into"/> one by one, <paramref name="rate"/> a second:
    /// a sensor's stand-in. Each is written to a temporary name and then renamed, as a careful sensor does.
    /// </summary>
    internal async Task ReplayAsync(List<FileInfo> files, string into, double rate, CancellationToken cancellationToken)
    {
        try
        {
            Log($"Replaying {files.Count} files from {files[0].DirectoryName}, {rate:0.##} a second, as a stand-in for a sensor.", OutputKind.Info);
            TimeSpan every = TimeSpan.FromSeconds(1 / rate);
            foreach (FileInfo file in files)
            {
                string target = Path.Combine(into, file.Name);
                file.CopyTo(target + ".part", overwrite: true);
                File.Move(target + ".part", target, overwrite: true);
                await Task.Delay(every, cancellationToken);
            }

            Log($"Replayed all {files.Count} files; the stream waits for more until stopped.", OutputKind.Info);
        }
        catch (OperationCanceledException)
        {
            // The stream ended.
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log($"The replay stopped: {e.Message}", OutputKind.Error);
        }
    }
}
