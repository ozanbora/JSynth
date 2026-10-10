using JSynth.Model;
using ReactiveUI;
using System;

namespace JSynth.ViewModels;

/// <summary>
/// A wire's breakpoint being edited, in the card opening from its dot: every time or on a condition, on or off, or
/// removed. Each change is the wire's at once.
/// </summary>
public sealed class BreakpointEditing : ReactiveObject
{
    private readonly Connection _wire;
    private readonly Action _close;
    private bool _everyTime;
    private string _condition;
    private bool _on;

    /// <param name="wire">The wire, with a breakpoint.</param>
    /// <param name="close">Closes the card, once the breakpoint is removed.</param>
    public BreakpointEditing(Connection wire, Action close)
    {
        (_wire, _close) = (wire, close);
        Breakpoint breakpoint = wire.Breakpoint ?? Breakpoint.Every;
        (_on, _condition) = (breakpoint.On, breakpoint.Condition);
        _everyTime = _condition.Trim().Length == 0;
        Title = $"Pause on {MainViewModel.DescribeWire(wire)}";
    }

    public string Title { get; }

    /// <summary>
    /// Whether it pauses every time a value comes; else when <see cref="Condition"/> holds.
    /// </summary>
    public bool EveryTime
    {
        get => _everyTime;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _everyTime, value);
            this.RaisePropertyChanged(nameof(OnCondition));
            Apply();
        }
    }

    public bool OnCondition
    {
        get => !_everyTime;
        set => EveryTime = !value;
    }

    /// <summary>
    /// Julia, true or false, of <c>x</c>, the value on the wire; kept while pausing every time is chosen, in case.
    /// </summary>
    public string Condition
    {
        get => _condition;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _condition, value ?? string.Empty);
            Apply();
        }
    }

    public bool On
    {
        get => _on;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _on, value);
            Apply();
        }
    }

    public void Remove()
    {
        MainViewModel.SetBreakpoint(_wire, null);
        _close();
    }

    private void Apply()
    {
        MainViewModel.SetBreakpoint(_wire, new Breakpoint(_on, _everyTime ? string.Empty : _condition));
    }
}
