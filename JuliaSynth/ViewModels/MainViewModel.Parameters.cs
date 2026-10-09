using Avalonia.Collections;
using Avalonia.Threading;
using JuliaSynth.Model;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace JuliaSynth.ViewModels;

/// <summary>
/// The parameters panel docked on the left of the canvas: the inputs put in it, to set with sliders and boxes, the design
/// running again a moment after each change.
/// </summary>
public partial class MainViewModel
{
    // How long after the last change in the panel the design runs again: long enough for a slider to settle.
    internal static TimeSpan RunAfterChange { get; set; } = TimeSpan.FromSeconds(0.5);

    private bool _isParametersOpen;
    private bool? _runOnChange;
    private DispatcherTimer? _runTimer;

    // The nodes whose names, wires or results the panel shows, followed for changes.
    private readonly List<Node> _followed = [];

    /// <summary>
    /// The inputs in the panel, top to bottom as on the canvas.
    /// </summary>
    public AvaloniaList<PanelParameter> Parameters { get; } = [];

    public bool HasParameters => Parameters.Count > 0;

    public bool IsParametersOpen
    {
        get => _isParametersOpen;
        set => this.RaiseAndSetIfChanged(ref _isParametersOpen, value);
    }

    /// <summary>
    /// Whether the design runs again a moment after a value is changed in the panel; kept in the settings, on unless
    /// turned off.
    /// </summary>
    public bool RunOnChange
    {
        get => _runOnChange ??= AppSettings.Load().RunOnChange;
        set
        {
            if (value == RunOnChange)
            {
                return;
            }

            _runOnChange = value;
            this.RaisePropertyChanged();
            AppSettings settings = AppSettings.Load();
            settings.RunOnChange = value;
            settings.Save();
        }
    }

    /// <summary>
    /// What the last run showed: each Display node's value, its first line.
    /// </summary>
    public string ParameterResults => string.Join("\n", Nodes.OfType<DisplayNode>()
        .Where(d => d.Result?.Status == Runtime.ReportStatus.Ok)
        .OrderBy(d => d.Row).ThenBy(d => d.Column)
        .Select(d => $"{ShownName(d)} = {d.Result!.Text.Split('\n')[0]}"));

    public ICommand ToggleParameters { get; private set; } = null!;

    private void CreateParameterCommands()
    {
        ToggleParameters = ReactiveCommand.Create(() => { IsParametersOpen = !IsParametersOpen; });

        Nodes.CollectionChanged += (_, _) => RefreshParameters();
        Connections.CollectionChanged += (_, _) => RefreshParameters();

        // A design opened with parameters shows them.
        DesignLoaded += () =>
        {
            RefreshParameters();
            IsParametersOpen = HasParameters;
        };
        RefreshParameters();
    }

    /// <summary>
    /// Builds the panel anew from the nodes put in it.
    /// </summary>
    private void RefreshParameters()
    {
        foreach (Node node in _followed)
        {
            node.PropertyChanged -= FollowedChanged;
        }

        _followed.Clear();
        foreach (PanelParameter parameter in Parameters)
        {
            parameter.Dispose();
        }

        Dictionary<string, string> variables = ScriptGenerator.NameVariables([.. Nodes.Where(n => n is not FrameNode)]);
        List<PanelParameter> shown = [.. Nodes
            .Where(n => n.CanBeInPanel && n.InPanel)
            .OrderBy(n => n.Row).ThenBy(n => n.Column)
            .Select(n => Describe(n, variables))];
        Parameters.Clear();
        Parameters.AddRange(shown);

        _followed.AddRange(Nodes.Where(n => n.CanBeInPanel || n is DisplayNode));
        foreach (Node node in _followed)
        {
            node.PropertyChanged += FollowedChanged;
        }

        this.RaisePropertyChanged(nameof(HasParameters));
        this.RaisePropertyChanged(nameof(ParameterResults));
    }

    /// <summary>
    /// <paramref name="node"/> in the panel: named after the parameter it is wired into, its source said in full.
    /// </summary>
    private PanelParameter Describe(Node node, Dictionary<string, string> variables)
    {
        string variable = variables.GetValueOrDefault(node.ID, "?");
        List<string> into = [];
        string? wiredName = null;
        foreach (Connection wire in Connections.Where(c => c.SourceID == node.ID && c.Output is null))
        {
            if (Nodes.FirstOrDefault(n => n.ID == wire.TargetID) is Node target && target.Ports.FirstOrDefault(p => p.ID == wire.ParameterID) is Parameter parameter)
            {
                wiredName ??= parameter.Name;
                into.Add($"{(target is Function f ? f.Name : target is GroupNode g ? g.Name : variables.GetValueOrDefault(target.ID, "?"))}({parameter.Name})");
            }
        }

        string source = into.Count == 0 ? variable : $"{variable} → {string.Join(", ", into)}";
        return new PanelParameter(node, wiredName ?? variable, source, ParameterEdited);
    }

    private static string ShownName(Node node) => node switch
    {
        Function f => f.Name,
        DisplayNode d => Connections.FirstOrDefault(c => c.TargetID == d.ID) is Connection wire
            && Nodes.FirstOrDefault(n => n.ID == wire.SourceID) is Node source ? ShownName(source) : "display",
        _ => node.GetType().Name,
    };

    private void FollowedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Node.InPanel))
        {
            // Put in the panel: it opens to show it.
            RefreshParameters();
            if (sender is Node { InPanel: true })
            {
                IsParametersOpen = true;
            }
        }
        else if (e.PropertyName == nameof(Node.Result) && sender is DisplayNode)
        {
            this.RaisePropertyChanged(nameof(ParameterResults));
        }
    }

    /// <summary>
    /// A value set in the panel: the design runs again once the values settle, if it is to.
    /// </summary>
    private void ParameterEdited(PanelParameter parameter)
    {
        if (!RunOnChange)
        {
            return;
        }

        _runTimer ??= new DispatcherTimer(RunAfterChange, DispatcherPriority.Background, (_, _) => RunChanged());
        _runTimer.Stop();
        _runTimer.Interval = RunAfterChange;
        _runTimer.Start();
    }

    private void RunChanged()
    {
        // Still running the last change: this one runs after it.
        if (IsRunning)
        {
            return;
        }

        _runTimer?.Stop();
        if (Julia is not null && ((ICommand)Run).CanExecute(null))
        {
            ((ICommand)Run).Execute(null);
        }
    }
}
