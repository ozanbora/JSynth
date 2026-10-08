using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Controls
{
    public partial class Node : UserControl
    {
        // Moves shorter than this are clicks, not drags.
        private const double DragThreshold = 3;

        private bool _isDragging;
        private bool _moved;
        private bool _selectOnlyOnClick;
        private Point _dragStart;
        private List<Node> _group = [];

        public Node() : base()
        {
            ClipToBounds = false;
            Width = 180;
            Height = 120;
        }

        private Model.Node? NodeModel => DataContext as Model.Node;

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (Parent is not Visual parent || NodeModel is not Model.Node model || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Don't let the canvas start panning or a selection box while a node is clicked.
            e.Handled = true;

            // Ctrl+click adds the node to, or removes it from, the selection.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                model.IsSelected = !model.IsSelected;
                return;
            }

            // Pressing a node of a multi-selection keeps the selection so the group can be dragged;
            // a plain click (no drag) then selects that node alone.
            _selectOnlyOnClick = model.IsSelected;
            if (!model.IsSelected)
            {
                MainViewModel.SelectOnly(model);
            }

            // The selected nodes go, and the nodes in the selected frames with them.
            List<Node> all = this.FindAncestorOfType<DesignerGrid>()?.GetVisualDescendants().OfType<Node>().ToList() ?? [this];
            List<Model.FrameNode> frames = all.Select(n => n.NodeModel).OfType<Model.FrameNode>().Where(f => f.IsSelected).ToList();
            _group = all
                .Where(n => n.NodeModel is Model.Node m && (m.IsSelected || frames.Any(f => f.Holds(m))))
                .ToList();

            // Capture first: taking capture from the child that was implicitly captured must not
            // be mistaken for losing it mid-drag in OnPointerCaptureLost.
            e.Pointer.Capture(this);
            _dragStart = e.GetPosition(parent);
            _isDragging = true;
            _moved = false;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!_isDragging || Parent is not Visual parent)
            {
                return;
            }

            Vector offset = e.GetPosition(parent) - _dragStart;
            _moved |= Math.Abs(offset.X) > DragThreshold || Math.Abs(offset.Y) > DragThreshold;
            if (!_moved)
            {
                return;
            }

            foreach (Node node in _group)
            {
                node.RenderTransform = new TranslateTransform(offset.X, offset.Y);
            }

            // Moving by render transform doesn't go through layout; keep the wires attached.
            this.FindAncestorOfType<DesignerGrid>()?.InvalidateWires();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_isDragging || Parent is not Visual parent)
            {
                return;
            }

            Vector offset = e.GetPosition(parent) - _dragStart;
            bool moved = _moved;
            List<Model.Node> models = _group.Select(n => n.NodeModel).OfType<Model.Node>().ToList();
            EndDrag();
            e.Pointer.Capture(null);

            if (!moved)
            {
                if (_selectOnlyOnClick)
                {
                    MainViewModel.SelectOnly(NodeModel);
                }

                return;
            }

            // Snap to the nearest grid cell by updating the models; the ItemsControl containers
            // bind Grid.Row/Grid.Column to them. Setting Grid.Row directly on a container would
            // override that binding and the model (and nodes.json) would never see the new position.
            // The whole group moves by the same amount, so it stays in shape at the canvas edges.
            int rowDelta = Math.Clamp((int)Math.Round(offset.Y / Constants.HEIGHT),
                -models.Min(m => m.Row), Constants.ROWS - 1 - models.Max(m => m.Row));
            int columnDelta = Math.Clamp((int)Math.Round(offset.X / Constants.WIDTH),
                -models.Min(m => m.Column), Constants.COLUMNS - 1 - models.Max(m => m.Column));

            foreach (Model.Node model in models)
            {
                model.Row += rowDelta;
                model.Column += columnDelta;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            // Capture taken away mid-drag (e.g. window lost focus): cancel and snap back.
            if (_isDragging)
            {
                EndDrag();
            }
        }

        /// <summary>
        /// Removes this node from the designer.
        /// </summary>
        protected void Delete_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is Model.Node node)
            {
                _ = MainViewModel.Nodes.Remove(node);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;
            foreach (Node node in _group)
            {
                node.RenderTransform = null;
            }

            RenderTransform = null;
            _group = [];
            this.FindAncestorOfType<DesignerGrid>()?.InvalidateWires();
        }
    }
}
