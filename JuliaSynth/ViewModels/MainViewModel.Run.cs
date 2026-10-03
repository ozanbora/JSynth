using Avalonia.Collections;
using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels;

public enum OutputKind
{
    /// <summary>
    /// Printed by the script.
    /// </summary>
    Output,

    /// <summary>
    /// Printed to standard error: warnings and errors.
    /// </summary>
    Error,

    /// <summary>
    /// From JuliaSynth: run started, finished, ...
    /// </summary>
    Info,

    /// <summary>
    /// A node's value.
    /// </summary>
    Value,

    /// <summary>
    /// A node that failed or wasn't run.
    /// </summary>
    Failure,
}

/// <summary>
/// What to do about packages a design needs that aren't installed.
/// </summary>
public enum PackageChoice
{
    Install,

    /// <summary>
    /// Run without them; the nodes needing them are skipped.
    /// </summary>
    RunAnyway,

    Cancel,
}

/// <summary>
/// A line in the output panel.
/// </summary>
public sealed record OutputEntry(string Text, OutputKind Kind)
{
    public bool IsError => Kind is OutputKind.Error or OutputKind.Failure;

    public bool IsInfo => Kind == OutputKind.Info;

    public bool IsValue => Kind == OutputKind.Value;
}

/// <summary>
/// Running the design with Julia: finding Julia, running the generated script, showing what came back.
/// </summary>
public partial class MainViewModel
{
    // The output panel keeps this many lines; a chatty script mustn't eat all memory.
    private const int MaxOutputLines = 5000;

    // Goes up with every change to the design, so results of a run started before it are dropped.
    private static int _designVersion;

    private JuliaInstallation? _julia;
    private bool _isLookingForJulia;
    private bool _isRunning;
    private bool _isOutputVisible;
    private CancellationTokenSource? _runCancellation;

    public AvaloniaList<OutputEntry> Output { get; } = [];

    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsOutputVisible
    {
        get => _isOutputVisible;
        set => this.RaiseAndSetIfChanged(ref _isOutputVisible, value);
    }

    public JuliaInstallation? Julia
    {
        get => _julia;
        internal set
        {
            bool same = value == _julia;
            _ = this.RaiseAndSetIfChanged(ref _julia, value);
            RaiseJuliaChanged();

            // It may be being installed: look again now and then, without saying so.
            if (value is null)
            {
                _ = WatchForJuliaAsync();
            }

            // Another Julia has packages of its own, and runs in a session of its own.
            _installedPackages.Clear();
            if (same)
            {
                return;
            }

            _session?.Dispose();
            _session = value is null ? null : new JuliaSession(value.Path);
            ForgetWarmUp();
            _ = WarmUpAsync();
        }
    }

    // The Julia runs go to, kept running between them so they needn't start Julia (and compile) again.
    private JuliaSession? _session;

    // Packages the session loaded ahead of a run, so it isn't asked again for each change to the design.
    private readonly HashSet<string> _warmedPackages = [];
    private bool _isWarm;

    /// <summary>
    /// Has the session start Julia, if it isn't running, and load the design's packages, while nothing
    /// waits for it; the next run starts right away.
    /// </summary>
    private async Task WarmUpAsync()
    {
        if (_session is not JuliaSession session)
        {
            return;
        }

        IReadOnlyList<string> packages = ScriptGenerator.Generate(Nodes, Connections, instrumented: false).Packages;
        if (_isWarm && packages.All(_warmedPackages.Contains))
        {
            return;
        }

        _warmedPackages.UnionWith(packages);
        _isWarm = true;
        try
        {
            await session.WarmUpAsync(JuliaSession.WarmUpScript(packages));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {
            // The run says what is wrong, if anything still is.
        }
    }

    /// <summary>
    /// The session's Julia ended, or will: the next warm-up starts its replacement.
    /// </summary>
    private void ForgetWarmUp()
    {
        _warmedPackages.Clear();
        _isWarm = false;
    }

    /// <summary>
    /// Ends the Julia runs go to; called when JuliaSynth closes.
    /// </summary>
    public void EndJulia()
    {
        _session?.Dispose();
    }

    /// <summary>
    /// Starts a new Julia for the next runs, without anything the earlier ones left behind (a package
    /// changed on disk, a method redefined globally, ...).
    /// </summary>
    public void RestartJulia()
    {
        IsOutputVisible = true;
        if (_session is null)
        {
            Log("Julia wasn't found, so there is nothing to restart.", OutputKind.Info);
            return;
        }

        _session.Restart();
        ForgetWarmUp();
        Log("Restarted Julia: the next run starts afresh.", OutputKind.Info);
        _ = WarmUpAsync();
    }

    /// <summary>
    /// "Julia 1.11.2", or why there is none.
    /// </summary>
    public string JuliaStatus => _isLookingForJulia ? "Looking for Julia…"
        : Julia is null ? "Julia not found"
        : $"Julia {Julia.Version}";

    public ReactiveCommand<RxVoid, RxVoid> Run { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> Stop { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> InstallPackages { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> Restart { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> ClearOutput { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> ToggleOutput { get; private set; } = null!;

    private void CreateRunCommands()
    {
        IObservable<bool> running = this.WhenAnyValue(x => x.IsRunning);
        Run = ReactiveCommand.CreateFromTask(RunDesignAsync, this.WhenAnyValue(x => x.IsRunning, busy => !busy));
        Stop = ReactiveCommand.Create(() => _runCancellation?.Cancel(), running);
        InstallPackages = ReactiveCommand.CreateFromTask(InstallMissingPackagesAsync, this.WhenAnyValue(x => x.IsRunning, busy => !busy));
        Restart = ReactiveCommand.Create(RestartJulia, this.WhenAnyValue(x => x.IsRunning, busy => !busy));

        // A design opened needs its packages loaded before it runs.
        DesignLoaded += () => _ = WarmUpAsync();
        ClearOutput = ReactiveCommand.Create(Output.Clear);
        ToggleOutput = ReactiveCommand.Create(() => { IsOutputVisible = !IsOutputVisible; });
    }

    /// <summary>
    /// Looks for Julia: the one picked before, else a usual place.
    /// </summary>
    public async Task FindJuliaAsync()
    {
        _isLookingForJulia = true;
        RaiseJuliaChanged();
        try
        {
            Julia = await Task.Run(() => JuliaLocator.FindAsync(AppSettings.Load().JuliaPath));
        }
        finally
        {
            _isLookingForJulia = false;
            RaiseJuliaChanged();
        }
    }

    /// <summary>
    /// Uses the Julia program at <paramref name="path"/> from now on; false when it isn't one.
    /// </summary>
    public async Task<bool> ChooseJuliaAsync(string path)
    {
        if (await Task.Run(() => JuliaLocator.ProbeAsync(path)) is not JuliaInstallation julia)
        {
            Log($"{path} is not a working Julia program (julia --version failed).", OutputKind.Error);
            IsOutputVisible = true;
            return false;
        }

        Julia = julia;
        AppSettings settings = AppSettings.Load();
        settings.JuliaPath = path;
        settings.Save();
        Log($"Using Julia {julia.Version} at {julia.Path}.", OutputKind.Info);
        return true;
    }

    /// <summary>
    /// Replaces the design with <paramref name="example"/>, as a new unsaved design, and says what it does.
    /// </summary>
    public void OpenExample(Example example)
    {
        IsStartPageOpen = false;
        Load(example.Build());
        History.Reset();
        FilePath = null;
        ScrollViewerOffset = Avalonia.Vector.Zero;

        IsOutputVisible = true;
        if (Output.Count > 0)
        {
            Log(string.Empty, OutputKind.Info);
        }

        Log($"Example: {example.Name}. {example.Description} Run it with F5.", OutputKind.Output);
    }

    /// <summary>
    /// The design as a plain Julia script, to save and run elsewhere.
    /// </summary>
    public static string ExportScript()
    {
        return ScriptGenerator.Generate(Nodes, Connections, instrumented: false).Text;
    }

    /// <summary>
    /// Asks whether to install the packages named, which the design needs but Julia can't load; the
    /// flag says whether running without them is an option. Set by the window; without it (in tests),
    /// runs go ahead without them.
    /// </summary>
    public Func<IReadOnlyList<string>, bool, Task<PackageChoice>>? AskToInstall { get; set; }

    // Packages known to load with the current Julia, so they aren't checked before every run.
    private readonly HashSet<string> _installedPackages = [];

    /// <summary>
    /// Where runs start (and relative paths in the design start from): the design's folder.
    /// </summary>
    private string RunFolder => _filePath is string path && Path.GetDirectoryName(path) is { Length: > 0 } directory
        ? directory
        : Environment.CurrentDirectory;

    /// <summary>
    /// Runs the whole design and shows every node's value (or error) on the node and in the output.
    /// Packages the design needs that aren't installed are offered for installing first.
    /// </summary>
    public async Task RunDesignAsync()
    {
        if (await StartAsync() is not (JuliaInstallation julia, CancellationTokenSource cancellation))
        {
            return;
        }

        try
        {
            string folder = RunFolder;
            ClearPlotFiles();
            JuliaSession session = _session!;
            JuliaScript script = ScriptGenerator.Generate(Nodes, Connections, instrumented: true, PlotFolder);
            int version = _designVersion;
            Dictionary<string, Node> nodes = Nodes.ToDictionary(n => n.ID);

            if (!await EnsurePackagesAsync(julia, script.Packages, folder, offerRunAnyway: true, cancellation.Token))
            {
                return;
            }

            // Results of the last run no longer apply once the run starts.
            foreach (Node node in Nodes)
            {
                node.Result = null;
            }

            void Show(Report report)
            {
                // Nodes changed since the run started would show values they don't have.
                if (version != _designVersion || !nodes.TryGetValue(report.Id, out Node? node) || !script.Variables.TryGetValue(report.Id, out string? variable))
                {
                    return;
                }

                node.Result = report;
                if (node is PlotNode plot && report.Status == ReportStatus.Ok)
                {
                    _ = LoadPlotAsync(plot, report.Text, version);
                }

                Log(report.Status switch
                {
                    ReportStatus.Ok when node is SaveFile => $"{variable}: saved to {report.Text}",
                    ReportStatus.Ok when node is PlotNode => $"{variable}: plotted",
                    ReportStatus.Ok => $"{variable} = {Shorten(ValueLine(report))}   ({report.Type})",
                    ReportStatus.Error => $"{variable}: {Shorten(report.Text.Split('\n')[0])}",
                    _ => $"{variable} not run: {Shorten(report.Text.Split('\n')[0])}",
                }, report.Status == ReportStatus.Ok ? OutputKind.Value : OutputKind.Failure);
            }

            Log($"Running with Julia {julia.Version} in {folder}…", OutputKind.Info);
            RunResult result = await session.RunAsync(
                script.Text,
                folder,
                line => Dispatcher.UIThread.Post(() => Log(line.Text, line.IsError ? OutputKind.Error : OutputKind.Output)),
                report => Dispatcher.UIThread.Post(() => Show(report)),
                cancellation.Token);

            // Stopping ended that Julia: have the next one ready for the next run.
            if (result.Cancelled)
            {
                ForgetWarmUp();
                _ = WarmUpAsync();
            }

            // Let the output still queued for the UI thread come first.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Log(result.Cancelled ? "Stopped."
                : result.ExitCode == 0 ? $"Finished in {result.Elapsed.TotalSeconds:0.0} s."
                : $"Julia stopped with exit code {result.ExitCode} after {result.Elapsed.TotalSeconds:0.0} s.",
                result.Succeeded ? OutputKind.Info : OutputKind.Error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Log($"Julia couldn't be started: {e.Message}", OutputKind.Error);
        }
        finally
        {
            Finish(cancellation);
        }
    }

    /// <summary>
    /// Where runs write the numbers of plots, one folder per running JuliaSynth.
    /// </summary>
    private static string PlotFolder { get; } = Path.Combine(Path.GetTempPath(), "JuliaSynth", $"plots-{Environment.ProcessId}");

    /// <summary>
    /// Empties <see cref="PlotFolder"/> for a new run; plots of the last one aren't shown any more.
    /// </summary>
    private static void ClearPlotFiles()
    {
        DeletePlotFiles();
        _ = Directory.CreateDirectory(PlotFolder);
    }

    /// <summary>
    /// Removes the plot files of this session; called when JuliaSynth closes.
    /// </summary>
    public static void DeletePlotFiles()
    {
        try
        {
            if (Directory.Exists(PlotFolder))
            {
                Directory.Delete(PlotFolder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the system's temp clean-up.
        }
    }

    /// <summary>
    /// Removes plot files left behind by JuliaSynth sessions that ended without cleaning up (a crash).
    /// </summary>
    public static void DeleteOldPlotFiles()
    {
        string root = Path.GetDirectoryName(PlotFolder)!;
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string folder in Directory.GetDirectories(root, "plots-*"))
        {
            if (!int.TryParse(Path.GetFileName(folder)["plots-".Length..], out int id) || IsRunning(id))
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // In use after all; next time.
            }
        }

        static bool IsRunning(int processId)
        {
            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Reads the numbers of a plot a run wrote to <paramref name="path"/>, off the UI thread, and shows them
    /// unless the design changed meanwhile.
    /// </summary>
    private async Task LoadPlotAsync(PlotNode plot, string path, int version)
    {
        try
        {
            PlotData data = await Task.Run(() => PlotData.Read(path));
            if (version == _designVersion)
            {
                plot.Plot = data;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log($"The plot couldn't be read: {e.Message}", OutputKind.Error);
        }
    }

    /// <summary>
    /// Checks the packages the design needs, and offers to install the missing ones.
    /// </summary>
    public async Task InstallMissingPackagesAsync()
    {
        if (await StartAsync() is not (JuliaInstallation julia, CancellationTokenSource cancellation))
        {
            return;
        }

        try
        {
            IReadOnlyList<string> packages = ScriptGenerator.Generate(Nodes, Connections, instrumented: false).Packages;
            if (packages.Count == 0)
            {
                Log("The design doesn't use any packages.", OutputKind.Info);
                return;
            }

            // Check even those that loaded before: this is where to look when one went missing.
            _installedPackages.Clear();
            IReadOnlyList<string> missing = await EnsurePackagesAsync(julia, packages, RunFolder, offerRunAnyway: false, cancellation.Token)
                ? packages.Where(p => !_installedPackages.Contains(p)).ToList()
                : packages;
            if (missing.Count == 0)
            {
                Log($"All packages the design needs are installed: {string.Join(", ", packages)}.", OutputKind.Info);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Log($"Julia couldn't be started: {e.Message}", OutputKind.Error);
        }
        finally
        {
            Finish(cancellation);
        }
    }

    /// <summary>
    /// Makes sure Julia can load <paramref name="packages"/>: finds those it can't and, if the user agrees,
    /// installs them. False when the user cancelled or stopped.
    /// </summary>
    private async Task<bool> EnsurePackagesAsync(JuliaInstallation julia, IReadOnlyList<string> packages, string folder, bool offerRunAnyway, CancellationToken cancellationToken)
    {
        List<string> unknown = packages.Where(p => !_installedPackages.Contains(p)).ToList();
        if (unknown.Count == 0)
        {
            return true;
        }

        JuliaPackages manager = new(julia.Path, _session!);
        IReadOnlyList<string> missing;
        try
        {
            Log($"Checking packages: {string.Join(", ", unknown)}…", OutputKind.Info);
            missing = await manager.FindMissingAsync(unknown, folder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopping the check ended the Julia it ran in: have the next one ready.
            ForgetWarmUp();
            _ = WarmUpAsync();
            Log("Stopped.", OutputKind.Info);
            return false;
        }
        catch (InvalidOperationException e)
        {
            // Not knowing is no reason not to run: nodes needing a missing package still say so.
            Log(e.Message, OutputKind.Error);
            return true;
        }

        _installedPackages.UnionWith(unknown.Except(missing));
        if (missing.Count == 0)
        {
            return true;
        }

        string names = string.Join(", ", missing);
        string them = missing.Count == 1 ? "it" : "them";
        Log($"Not installed: {names}.", OutputKind.Error);
        PackageChoice choice = AskToInstall is null ? PackageChoice.RunAnyway : await AskToInstall(missing, offerRunAnyway);
        if (choice == PackageChoice.Cancel || (choice == PackageChoice.RunAnyway && !offerRunAnyway))
        {
            Log($"Not installing. To install {them} yourself, run in Julia: {JuliaPackages.InstallScript(missing).TrimEnd().Replace("\n", "; ")}", OutputKind.Info);
            return false;
        }

        if (choice == PackageChoice.RunAnyway)
        {
            Log($"Running without {names}: the nodes needing {them} are skipped.", OutputKind.Info);
            return true;
        }

        Log($"Installing {names} with Julia's package manager; the first time, this can take a few minutes…", OutputKind.Info);
        RunResult result = await manager.InstallAsync(missing, folder, line => Dispatcher.UIThread.Post(() => Log(line.Text, OutputKind.Output)), cancellationToken);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (result.Cancelled)
        {
            Log("Installing stopped.", OutputKind.Info);
            return false;
        }

        if (!result.Succeeded)
        {
            Log($"Installing {names} failed (see above); the nodes needing {them} are skipped.", OutputKind.Error);
            return true;
        }

        _installedPackages.UnionWith(missing);

        // Loaded afresh by a new Julia, rather than by one that looked for them before they were there.
        _session?.Restart();
        ForgetWarmUp();
        Log($"Installed {names} in {result.Elapsed.TotalSeconds:0} s.", OutputKind.Info);
        return true;
    }

    /// <summary>
    /// Finds Julia and marks a run (or package work) as going on, so Stop can end it; null without Julia.
    /// </summary>
    private async Task<(JuliaInstallation, CancellationTokenSource)?> StartAsync()
    {
        IsOutputVisible = true;

        // A blank line between runs.
        if (Output.Count > 0)
        {
            Log(string.Empty, OutputKind.Info);
        }

        if (Julia is null)
        {
            await FindJuliaAsync();
        }

        if (Julia is not JuliaInstallation julia)
        {
            Log("Julia wasn't found. Install it from https://julialang.org/downloads (juliaup is easiest), or pick julia.exe with Run → Choose Julia…", OutputKind.Error);
            return null;
        }

        CancellationTokenSource cancellation = new();
        _runCancellation = cancellation;
        IsRunning = true;
        return (julia, cancellation);
    }

    private void Finish(CancellationTokenSource cancellation)
    {
        _runCancellation = null;
        cancellation.Dispose();
        IsRunning = false;
    }

    /// <summary>
    /// The line of a value's text showing the value itself: "3.375", or the first elements of an array.
    /// </summary>
    private static string ValueLine(Report report)
    {
        string[] lines = report.Text.Split('\n');
        if (lines.Length == 1)
        {
            return lines[0];
        }

        // Arrays show a summary line ("3-element Vector{Float64}:") before their elements. A vector's
        // elements fit on one line; a matrix's rows (or a sparse matrix's dot picture) don't.
        string summary = lines[0].TrimEnd(':');
        return JuliaTypes.Dimensions(JuliaTypes.Split(report.Type).Shape) == 1
            ? $"{summary}: {string.Join(", ", lines.Skip(1).Take(4).Select(l => l.Trim()))}{(lines.Length > 5 ? ", …" : string.Empty)}"
            : summary;
    }

    private static string Shorten(string text)
    {
        return text.Length <= 160 ? text : text[..157] + "…";
    }

    private void Log(string text, OutputKind kind)
    {
        Output.Add(new OutputEntry(text, kind));
        if (Output.Count > MaxOutputLines)
        {
            Output.RemoveRange(0, Output.Count - MaxOutputLines);
        }
    }

    /// <summary>
    /// Something in the design changed: remember it for undo, and drop results that may no longer hold.
    /// </summary>
    private static void DesignChanged()
    {
        History.ScheduleCommit();
        _designVersion++;
        foreach (Node node in Nodes.Where(n => n.HasResult))
        {
            node.Result = null;
        }
    }
}
