using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using JuliaSynth.Model;

namespace JuliaSynth.Controls;

/// <summary>
/// A port wires attach to. Its data context tells what it is: a <see cref="Model.Parameter"/> for a
/// function input, the node itself for a node's output.
/// </summary>
public partial class Connector : UserControl
{
    private DesignerGrid? _grid;
    private bool _isWiring;

    public Connector()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The parameter this input port feeds, or null for an output port.
    /// </summary>
    public Parameter? Parameter => DataContext as Parameter;

    public bool IsInput => Parameter is not null;

    /// <summary>
    /// The node the port belongs to.
    /// </summary>
    public Model.Node? Owner => this.FindAncestorOfType<Node>()?.DataContext as Model.Node;

    public Point CenterIn(Visual visual)
    {
        return this.TranslatePoint(new Point(Bounds.Width / 2, Bounds.Height / 2), visual) ?? default;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _grid = this.FindAncestorOfType<DesignerGrid>();
        _grid?.RegisterConnector(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _grid?.UnregisterConnector(this);
        _grid = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_grid == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Set before capturing: capture moving here from elsewhere must not cancel this drag.
        _isWiring = true;
        e.Pointer.Capture(this);
        _grid.BeginWire(this, e.GetPosition(_grid));

        // Wiring, not dragging the node.
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isWiring && _grid != null)
        {
            _grid.UpdateWire(e.GetPosition(_grid));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isWiring || _grid == null)
        {
            return;
        }

        _isWiring = false;
        _grid.EndWire(e.GetPosition(_grid));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // Capture taken away mid-drag (e.g. window lost focus): drop the wire being dragged.
        if (_isWiring)
        {
            _isWiring = false;
            _grid?.CancelWire();
        }
    }
}
