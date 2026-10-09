using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using JuliaSynth.ViewModels;
using System;
using System.Linq;

namespace JuliaSynth.Controls
{
    /// <summary>
    /// Selecting nodes with a box Ctrl+dragged on the empty canvas (Ctrl+Shift: added to the selection), and a wire
    /// with a click on it. A plain drag there pans the canvas, and a plain click clears the selection: see MainView.
    /// Clicks on nodes are handled by the nodes themselves.
    /// </summary>
    public partial class DesignerGrid
    {
        private const double BoxThreshold = 3;

        // The blue of a selected node's border.
        private static readonly Color SelectionColor = Color.Parse("#6F9FD8");

        private static readonly IBrush BoxFill = new ImmutableSolidColorBrush(SelectionColor, 0.12);
        private static readonly IPen BoxPen = new ImmutablePen(new ImmutableSolidColorBrush(SelectionColor), 1);

        private SelectionRenderer? _selectionRenderer;
        private bool _isSelecting;
        private bool _addToSelection;
        private Point _boxStart;
        private Point _boxEnd;

        private Rect SelectionBox => new Rect(_boxStart, _boxEnd).Normalize();

        private bool IsBoxVisible => _isSelecting
            && (Math.Abs(_boxEnd.X - _boxStart.X) > BoxThreshold || Math.Abs(_boxEnd.Y - _boxStart.Y) > BoxThreshold);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Keyboard shortcuts (Delete, Ctrl+A) need the focus somewhere in the window.
            _ = Focus();

            // A plain drag pans the canvas (the scroll viewer's, and a click there clears the selection), unless it
            // starts on a wire, which selects the wire; Ctrl selects nodes.
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (BreakpointAt(e.GetPosition(this)) is Model.Connection paused)
                {
                    EditBreakpoint(paused);
                    e.Handled = true;
                }
                else if (WireAt(e.GetPosition(this)) is Model.Connection wire)
                {
                    MainViewModel.SelectedConnection = wire;
                    e.Handled = true;
                }

                return;
            }

            _isSelecting = true;
            _addToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _boxStart = _boxEnd = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isSelecting)
            {
                _boxEnd = e.GetPosition(this);
                _selectionRenderer?.InvalidateVisual();
            }

            // Over the empty canvas, not a node: what a wire there carries.
            bool onNode = e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<Node>().Any();
            PeekWire(!_isSelecting && !onNode ? e.GetPosition(this) : null);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            PeekWire(null);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_isSelecting)
            {
                return;
            }

            _boxEnd = e.GetPosition(this);
            bool isBox = IsBoxVisible;
            Rect box = SelectionBox;
            _isSelecting = false;
            e.Pointer.Capture(null);
            _selectionRenderer?.InvalidateVisual();

            if (!isBox)
            {
                // A Ctrl+click on the empty canvas.
                if (!_addToSelection)
                {
                    MainViewModel.SelectOnly(null);
                }

                return;
            }

            foreach (Node node in this.GetVisualDescendants().OfType<Node>())
            {
                if (node.DataContext is not Model.Node model)
                {
                    continue;
                }

                Point topLeft = node.TranslatePoint(default, this) ?? default;
                // A frame only when the box goes all around it: a box drawn in one is for the nodes in it.
                Rect bounds = new(topLeft, node.Bounds.Size);
                bool inBox = model is Model.FrameNode ? box.Contains(bounds) : bounds.Intersects(box);
                if (inBox || !_addToSelection)
                {
                    model.IsSelected = inBox || (_addToSelection && model.IsSelected);
                }
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            if (_isSelecting)
            {
                _isSelecting = false;
                _selectionRenderer?.InvalidateVisual();
            }
        }

        private SelectionRenderer EnsureSelectionRenderer()
        {
            if (_selectionRenderer == null)
            {
                // Above the nodes.
                _selectionRenderer = new SelectionRenderer(this) { ZIndex = 100, IsHitTestVisible = false };
                VisualChildren.Add(_selectionRenderer);
            }

            return _selectionRenderer;
        }

        private sealed class SelectionRenderer(DesignerGrid grid) : Control
        {
            public override void Render(DrawingContext context)
            {
                if (grid.IsBoxVisible)
                {
                    context.DrawRectangle(BoxFill, BoxPen, grid.SelectionBox);
                }
            }
        }
    }
}
