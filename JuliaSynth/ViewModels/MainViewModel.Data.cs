using ReactiveUI;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels;

/// <summary>
/// Downloading the data of an example that isn't bundled (see <see cref="ExampleData"/>), with the
/// progress on a banner over the canvas.
/// </summary>
public partial class MainViewModel
{
    private string? _downloadText;
    private double _downloadProgress;

    /// <summary>
    /// What downloads example data; tests stand in for the internet with it.
    /// </summary>
    internal static HttpClient Http { get; set; } = new() { Timeout = TimeSpan.FromMinutes(2) };

    public bool IsDownloading => _downloadText is not null;

    /// <summary>
    /// "Downloading Boreas radar scans: 3 of 12 files…", while downloading.
    /// </summary>
    public string DownloadText => _downloadText ?? string.Empty;

    /// <summary>
    /// How far the download is, from 0 to 100.
    /// </summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
    }

    /// <summary>
    /// Downloads <paramref name="set"/> of <paramref name="data"/> into <paramref name="folder"/>, saying how it goes; Stop ends it.
    /// True once every file is there.
    /// </summary>
    public async Task<bool> DownloadExampleDataAsync(ExampleData data, ExampleDataSet set, string folder)
    {
        if (IsRunning)
        {
            return false;
        }

        CancellationTokenSource cancellation = new();
        _runCancellation = cancellation;
        IsRunning = true;
        IsOutputVisible = true;
        if (Output.Count > 0)
        {
            Log(string.Empty, OutputKind.Info);
        }

        Log($"Downloading {data.Name}, {set.Name.ToLowerInvariant()} set ({set.Size}), to {folder}…", OutputKind.Info);
        void Show(int done)
        {
            _downloadText = $"Downloading {data.Name} ({set.Name.ToLowerInvariant()}): {done} of {set.Files.Count} files. Stop ends it; what is downloaded stays for next time.";
            DownloadProgress = 100.0 * done / set.Files.Count;
            this.RaisePropertyChanged(nameof(IsDownloading));
            this.RaisePropertyChanged(nameof(DownloadText));
        }

        Show(0);
        try
        {
            await data.DownloadAsync(Http, folder, set, Show, cancellation.Token);
            Log($"Downloaded {data.Name} to {folder}. {data.Credit}", OutputKind.Info);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log("Downloading stopped.", OutputKind.Info);
            return false;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // A timeout comes as a cancellation nobody asked for.
            Log($"{data.Name} couldn't be downloaded: {(e is OperationCanceledException ? "the server didn't answer in time." : e.Message)}", OutputKind.Error);
            return false;
        }
        finally
        {
            _downloadText = null;
            this.RaisePropertyChanged(nameof(IsDownloading));
            this.RaisePropertyChanged(nameof(DownloadText));
            Finish(cancellation);
        }
    }
}
