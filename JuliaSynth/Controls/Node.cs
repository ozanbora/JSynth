using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using JuliaSynth.ViewModels;
using System;

namespace JuliaSynth.Controls
{
    public partial class Node : UserControl
    {
        private bool _isDragging;
        private Point _dragStart;

        public Node() : base()
        {
            ClipToBounds = false;
            Width = 180;
            Height = 120;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (Parent is not Visual parent || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Capture first: taking capture from the child that was implicitly captured must not
            // be mistaken for losing it mid-drag in OnPointerCaptureLost.
            e.Pointer.Capture(this);
            _dragStart = e.GetPosition(parent);
            _isDragging = true;

            // Don't let the canvas start panning while a node is being dragged.
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!_isDragging || Parent is not Visual parent)
            {
                return;
            }

            Vector offset = e.GetPosition(parent) - _dragStart;
            RenderTransform = new TranslateTransform(offset.X, offset.Y);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_isDragging || Parent is not Visual parent)
            {
                return;
            }

            Vector offset = e.GetPosition(parent) - _dragStart;
            EndDrag();
            e.Pointer.Capture(null);

            // Snap to the nearest grid cell by updating the model; the ItemsControl container
            // binds Grid.Row/Grid.Column to it. Setting Grid.Row directly on the container would
            // override that binding and the model (and nodes.json) would never see the new position.
            if (DataContext is Model.Node node)
            {
                int rowDelta = (int)Math.Round(offset.Y / Constants.HEIGHT);
                int columnDelta = (int)Math.Round(offset.X / Constants.WIDTH);

                node.Row = Math.Clamp(node.Row + rowDelta, 0, Constants.ROWS - 1);
                node.Column = Math.Clamp(node.Column + columnDelta, 0, Constants.COLUMNS - 1);
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
            RenderTransform = null;
        }
    }
}
