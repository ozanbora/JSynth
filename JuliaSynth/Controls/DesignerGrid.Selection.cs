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
    /// Selecting nodes with a box dragged on the empty canvas; a click on the empty canvas clears
    /// the selection. Clicks on nodes are handled by the nodes themselves.
    /// </summary>
    public partial class DesignerGrid
    {
        private const double BoxThreshold = 3;

        private static readonly IBrush BoxFill = new ImmutableSolidColorBrush(Color.Parse("#0078D4"), 0.12);
        private static readonly IPen BoxPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#0078D4")), 1);

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

            // Ctrl+drag pans the canvas (handled by the scroll viewer); leave it alone.
            if (e.Handled || e.KeyModifiers.HasFlag(KeyModifiers.Control) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Keyboard shortcuts (Delete, Ctrl+A) need the focus somewhere in the window.
            _ = Focus();

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
                // A plain click on the empty canvas.
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
                bool inBox = new Rect(topLeft, node.Bounds.Size).Intersects(box);
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
