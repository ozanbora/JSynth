using JuliaSynth.Model;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Linq;

namespace JuliaSynth.ViewModels;

/// <summary>
/// The function panel docked beside the designer: a function's settings and code, open while you
/// work on the rest of the design. Other nodes keep their small flyouts.
/// </summary>
public partial class MainViewModel
{
    private Function? _inspected;

    /// <summary>
    /// A node or a wire was selected or unselected.
    /// </summary>
    internal static event Action? SelectionChanged;

    /// <summary>
    /// The nodes were replaced by a design's: opened, undone, redone or new.
    /// </summary>
    private static event Action? DesignLoaded;

    // Set while a design replaces the nodes, when they are all removed for a moment.
    private static bool _loading;

    /// <summary>
    /// The function shown in the panel; null when it is closed.
    /// </summary>
    public Function? Inspected
    {
        get => _inspected;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _inspected, value);
            this.RaisePropertyChanged(nameof(IsInspectorOpen));
        }
    }

    public bool IsInspectorOpen => Inspected is not null;

    public ReactiveCommand<RxVoid, RxVoid> CloseInspector { get; private set; } = null!;

    /// <summary>
    /// Opens <paramref name="function"/> in the panel, and selects it so it is easy to spot.
    /// </summary>
    public void Inspect(Function function)
    {
        Inspected = function;
        SelectOnly(function);
    }

    private void CreateInspectorCommands()
    {
        CloseInspector = ReactiveCommand.Create(() => { Inspected = null; });

        // Deleting the function closes the panel.
        Nodes.CollectionChanged += (_, e) =>
        {
            if (!_loading && Inspected is Function shown && e.OldItems?.Contains(shown) == true)
            {
                Inspected = null;
            }
        };

        // Undo, redo and opening a design replace every node (clearing first): once the new ones are
        // in, follow the function to its new self, or close when it is gone.
        DesignLoaded += () =>
        {
            if (Inspected is Function shown && !Nodes.Contains(shown))
            {
                Inspected = Nodes.OfType<Function>().FirstOrDefault(f => f.ID == shown.ID);
            }
        };

        // While open, the panel shows whichever function is selected on its own.
        SelectionChanged += () =>
        {
            if (Inspected is not null && Nodes.Where(n => n.IsSelected).ToList() is [Function selected] && selected != Inspected)
            {
                Inspected = selected;
            }
        };
    }
}
