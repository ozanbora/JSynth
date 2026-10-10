using JSynth.Runtime;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JSynth.ViewModels;

/// <summary>
/// The start page: what is there before a design is, with Julia's state and the examples, and help
/// installing Julia when it is missing.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// Where to read about installing Julia.
    /// </summary>
    public const string JuliaInstallPage = "https://julialang.org/install/";

    // How often to look again for Julia while it is missing: it may be being installed.
    private static readonly TimeSpan JuliaWatchInterval = TimeSpan.FromSeconds(3);

    private bool _isStartPageOpen;
    private bool _isWatchingForJulia;

    /// <summary>
    /// Shown over the canvas.
    /// </summary>
    public bool IsStartPageOpen
    {
        get => _isStartPageOpen;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _isStartPageOpen, value);
            this.RaisePropertyChanged(nameof(IsCanvasHintVisible));
        }
    }

    /// <summary>
    /// How to start, on an empty canvas.
    /// </summary>
    public bool IsCanvasHintVisible => !IsStartPageOpen && Nodes.Count == 0;

    public IReadOnlyList<Example> StartExamples => Examples.All;

    public bool IsJuliaFound => Julia is not null;

    public bool IsLookingForJulia => Julia is null && _isLookingForJulia;

    /// <summary>
    /// Julia was looked for and isn't there.
    /// </summary>
    public bool IsJuliaMissing => Julia is null && !_isLookingForJulia;

    /// <summary>
    /// The command installing Julia with juliaup, its installer, on this system.
    /// </summary>
    public static string JuliaInstallCommand => OperatingSystem.IsWindows()
        ? "winget install --name Julia --id 9NJNWW8PVKMN -e -s msstore"
        : "curl -fsSL https://install.julialang.org | sh";

    public ReactiveCommand<RxVoid, RxVoid> ShowStartPage { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> CloseStartPage { get; private set; } = null!;

    private void CreateStartCommands()
    {
        ShowStartPage = ReactiveCommand.Create(() => { IsStartPageOpen = true; });
        CloseStartPage = ReactiveCommand.Create(() => { IsStartPageOpen = false; });
        Nodes.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(IsCanvasHintVisible));
    }

    /// <summary>
    /// Looks for Julia when JSynth starts; without it, the start page says how to install it.
    /// </summary>
    private async Task FindJuliaAtStartAsync()
    {
        await FindJuliaAsync();
        if (Julia is null)
        {
            IsStartPageOpen = true;
        }
    }

    private void RaiseJuliaChanged()
    {
        this.RaisePropertyChanged(nameof(JuliaStatus));
        this.RaisePropertyChanged(nameof(IsJuliaFound));
        this.RaisePropertyChanged(nameof(IsLookingForJulia));
        this.RaisePropertyChanged(nameof(IsJuliaMissing));
    }

    /// <summary>
    /// While Julia is missing, looks for it again every few seconds, without saying so, until it is found
    /// (installed meanwhile) or picked.
    /// </summary>
    private async Task WatchForJuliaAsync()
    {
        if (_isWatchingForJulia)
        {
            return;
        }

        _isWatchingForJulia = true;
        try
        {
            while (Julia is null)
            {
                await Task.Delay(JuliaWatchInterval);
                if (Julia is null && !_isLookingForJulia
                    && await Task.Run(() => JuliaLocator.FindAsync(AppSettings.Load().JuliaPath)) is JuliaInstallation julia
                    && Julia is null)
                {
                    Julia = julia;
                    Log($"Found Julia {julia.Version} at {julia.Path}.", OutputKind.Info);
                }
            }
        }
        finally
        {
            _isWatchingForJulia = false;
        }
    }
}
