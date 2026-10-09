using JuliaSynth.Model;
using JuliaSynth.Runtime;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace JuliaSynth.ViewModels;

/// <summary>
/// Breakpoints on wires: a run pauses when a value comes on one (every time, or when its condition holds), showing
/// everything worked out so far, until told to go on to the next breakpoint, or to this wire's next value.
/// </summary>
public partial class MainViewModel
{
    private bool _isPaused;
    private string _pausedText = string.Empty;

    // The stop file of the run going on: its resume file, next to it, tells a paused run to go on.
    private string? _runStopFile;

    /// <summary>
    /// A wire's breakpoint was set, changed or removed, or a run paused at one or went on.
    /// </summary>
    internal static event Action? BreakpointsChanged;

    /// <summary>
    /// The wire the run is paused at; null when not paused.
    /// </summary>
    public static Connection? PausedWire { get; private set; }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _isPaused, value);
            this.RaisePropertyChanged(nameof(IsStreamBannerVisible));
        }
    }

    /// <summary>
    /// For the banner while paused: where, how far the run got, and the value.
    /// </summary>
    public string PausedText
    {
        get => _pausedText;
        private set => this.RaiseAndSetIfChanged(ref _pausedText, value);
    }

    /// <summary>
    /// The stream's banner shows while streaming, but for the paused one.
    /// </summary>
    public bool IsStreamBannerVisible => IsStreaming && !IsPaused;

    /// <summary>
    /// Goes on from a pause, to the next breakpoint the run comes to.
    /// </summary>
    public ICommand Continue { get; private set; } = null!;

    /// <summary>
    /// Goes on from a pause, to the next value on the wire paused at, whatever its condition.
    /// </summary>
    public ICommand ToNext { get; private set; } = null!;

    /// <summary>
    /// F5: goes on from a pause, else runs the design.
    /// </summary>
    public ICommand RunOrContinue { get; private set; } = null!;

    /// <summary>
    /// F9: a breakpoint on the selected wire, or none if it had one.
    /// </summary>
    public ICommand ToggleBreakpoint { get; private set; } = null!;

    private void CreateBreakpointCommands()
    {
        IObservable<bool> paused = this.WhenAnyValue(x => x.IsPaused);
        Continue = ReactiveCommand.Create(() => Resume("continue"), paused);
        ToNext = ReactiveCommand.Create(() => Resume("next"), paused);
        RunOrContinue = ReactiveCommand.Create(() =>
        {
            if (IsPaused)
            {
                Resume("continue");
            }
            else if (((ICommand)Run).CanExecute(null))
            {
                ((ICommand)Run).Execute(null);
            }
        });
        ToggleBreakpoint = ReactiveCommand.Create(() =>
        {
            if (SelectedConnection is Connection wire)
            {
                SetBreakpoint(wire, wire.Breakpoint is null ? Breakpoint.Every : null);
            }
        }, this.WhenAnyValue(x => x.IsTypingText, typing => !typing));
    }

    /// <summary>
    /// Sets <paramref name="wire"/>'s breakpoint; null removes it. It is part of the design, kept in its history; it
    /// changes nothing worked out.
    /// </summary>
    public static void SetBreakpoint(Connection wire, Breakpoint? breakpoint)
    {
        if (wire.Breakpoint == breakpoint)
        {
            return;
        }

        wire.Breakpoint = breakpoint;
        History.ScheduleCommit();
        BreakpointsChanged?.Invoke();
    }

    /// <summary>
    /// The run paused at the wire <paramref name="report"/> is about: say where, how far the run got, and the value.
    /// </summary>
    private void PausedAt(Report report, JuliaScript script)
    {
        PausedWire = Connections.FirstOrDefault(c => c.Key == report.Id);
        string where = PausedWire is Connection wire ? DescribeWire(wire, script.Variables) : "a wire";
        string far = IsStreaming && StreamStatus.Length > 0 ? $", {StreamStatus}" : string.Empty;
        string[] lines = report.Text.Split('\n');
        string value = lines.Length == 1 ? lines[0] : $"{lines[0]} …";
        string condition = PausedWire?.Breakpoint is { Condition.Length: > 0 } breakpoint ? $" ({breakpoint.Condition.Trim()})" : string.Empty;
        PausedText = $"Paused at {where}{far}: x = {value}{condition}. The nodes after it wait; the values so far show on the nodes and wires.";
        IsPaused = true;
        Log($"Paused at {where}{far}: x = {value}   ({report.Type})", OutputKind.Info);
        BreakpointsChanged?.Invoke();
    }

    /// <summary>
    /// "estimate → window1(value)": the wire's source and the input it goes into, as a script names them.
    /// </summary>
    public static string DescribeWire(Connection wire) => DescribeWire(wire, ScriptGenerator.NameVariables([.. Nodes.Where(n => n is not FrameNode)]));

    private static string DescribeWire(Connection wire, System.Collections.Generic.IReadOnlyDictionary<string, string> variables)
    {
        Node? source = Nodes.FirstOrDefault(n => n.ID == wire.SourceID);
        Node? target = Nodes.FirstOrDefault(n => n.ID == wire.TargetID);
        string Name(Node? node) => node is Function f ? f.Name : node is GroupNode g ? g.Name : node is not null && variables.TryGetValue(node.ID, out string? variable) ? variable : "?";
        string output = wire.Output is null ? string.Empty : $".{wire.Output}";
        string input = target?.Ports.FirstOrDefault(p => p.ID == wire.ParameterID)?.Name ?? "?";
        return $"{Name(source)}{output} → {Name(target)}({input})";
    }

    /// <summary>
    /// Tells the paused run to go on: <paramref name="how"/> "continue", or "next" for this wire's next value.
    /// </summary>
    private void Resume(string how)
    {
        if (!IsPaused || _runStopFile is not string stop)
        {
            return;
        }

        try
        {
            // Written whole, then named: the run never reads half of it.
            string resume = Breakpoint.ResumeFile(stop);
            File.WriteAllText(resume + ".part", how);
            File.Move(resume + ".part", resume, overwrite: true);
            EndPause();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"The run couldn't be told to go on: {e.Message}", OutputKind.Error);
        }
    }

    private void EndPause()
    {
        IsPaused = false;
        PausedWire = null;
        BreakpointsChanged?.Invoke();
    }
}
