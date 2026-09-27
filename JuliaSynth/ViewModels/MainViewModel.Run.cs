using Avalonia.Collections;
using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _julia, value);
            this.RaisePropertyChanged(nameof(JuliaStatus));
        }
    }

    /// <summary>
    /// "Julia 1.11.2", or why there is none.
    /// </summary>
    public string JuliaStatus => _isLookingForJulia ? "Looking for Julia…"
        : Julia is null ? "Julia not found"
        : $"Julia {Julia.Version}";

    public ReactiveCommand<Unit, Unit> Run { get; private set; } = null!;

    public ReactiveCommand<Unit, Unit> Stop { get; private set; } = null!;

    public ReactiveCommand<Unit, Unit> ClearOutput { get; private set; } = null!;

    public ReactiveCommand<Unit, Unit> ToggleOutput { get; private set; } = null!;

    private void CreateRunCommands()
    {
        IObservable<bool> running = this.WhenAnyValue(x => x.IsRunning);
        Run = ReactiveCommand.CreateFromTask(RunDesignAsync, running.Select(r => !r));
        Stop = ReactiveCommand.Create(() => _runCancellation?.Cancel(), running);
        ClearOutput = ReactiveCommand.Create(Output.Clear);
        ToggleOutput = ReactiveCommand.Create(() => { IsOutputVisible = !IsOutputVisible; });
    }

    /// <summary>
    /// Looks for Julia: the one picked before, else a usual place.
    /// </summary>
    public async Task FindJuliaAsync()
    {
        _isLookingForJulia = true;
        this.RaisePropertyChanged(nameof(JuliaStatus));
        try
        {
            Julia = await Task.Run(() => JuliaLocator.FindAsync(AppSettings.Load().JuliaPath));
        }
        finally
        {
            _isLookingForJulia = false;
            this.RaisePropertyChanged(nameof(JuliaStatus));
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
    /// The design as a plain Julia script, to save and run elsewhere.
    /// </summary>
    public static string ExportScript()
    {
        return ScriptGenerator.Generate(Nodes, Connections, instrumented: false).Text;
    }

    /// <summary>
    /// Runs the whole design and shows every node's value (or error) on the node and in the output.
    /// </summary>
    public async Task RunDesignAsync()
    {
        IsOutputVisible = true;
        if (Julia is null)
        {
            await FindJuliaAsync();
        }

        if (Julia is not JuliaInstallation julia)
        {
            Log("Julia wasn't found. Install it from https://julialang.org/downloads (juliaup is easiest), or pick julia.exe with Run → Choose Julia…", OutputKind.Error);
            return;
        }

        // Results of the last run no longer apply once the run starts.
        foreach (Node node in Nodes)
        {
            node.Result = null;
        }

        JuliaScript script = ScriptGenerator.Generate(Nodes, Connections, instrumented: true);
        int version = _designVersion;
        Dictionary<string, Node> nodes = Nodes.ToDictionary(n => n.ID);

        JuliaRunner runner = new(julia.Path);
        runner.Output += line => Dispatcher.UIThread.Post(() => Log(line.Text, line.IsError ? OutputKind.Error : OutputKind.Output));
        runner.Reported += report => Dispatcher.UIThread.Post(() => Show(report));

        void Show(Report report)
        {
            // Nodes changed since the run started would show values they don't have.
            if (version != _designVersion || !nodes.TryGetValue(report.Id, out Node? node) || !script.Variables.TryGetValue(report.Id, out string? variable))
            {
                return;
            }

            node.Result = report;
            Log(report.Status switch
            {
                ReportStatus.Ok => $"{variable} = {Shorten(ValueLine(report))}   ({report.Type})",
                ReportStatus.Error => $"{variable}: {Shorten(report.Text.Split('\n')[0])}",
                _ => $"{variable} not run: {Shorten(report.Text.Split('\n')[0])}",
            }, report.Status == ReportStatus.Ok ? OutputKind.Value : OutputKind.Failure);
        }

        string folder = _filePath is string path && Path.GetDirectoryName(path) is { Length: > 0 } directory
            ? directory
            : Environment.CurrentDirectory;

        // A blank line between runs.
        if (Output.Count > 0)
        {
            Log(string.Empty, OutputKind.Info);
        }

        Log($"Running with Julia {julia.Version} in {folder}…", OutputKind.Info);
        IsRunning = true;
        using CancellationTokenSource cancellation = new();
        _runCancellation = cancellation;
        try
        {
            RunResult result = await runner.RunAsync(script.Text, folder, cancellation.Token);

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
            _runCancellation = null;
            IsRunning = false;
        }
    }

    /// <summary>
    /// The line of a value's text showing the value itself: "3.375", or the first elements of an array.
    /// </summary>
    private static string ValueLine(Report report)
    {
        string[] lines = report.Text.Split('\n');

        // Arrays show a summary line ("3-element Vector{Float64}:") before their elements.
        return lines.Length > 1 ? $"{lines[0].TrimEnd(':')}: {string.Join(", ", lines.Skip(1).Take(4).Select(l => l.Trim()))}{(lines.Length > 5 ? ", …" : string.Empty)}" : lines[0];
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
