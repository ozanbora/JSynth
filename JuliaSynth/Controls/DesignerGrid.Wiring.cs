using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using JuliaSynth.Converters;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace JuliaSynth.Controls
{
    /// <summary>
    /// Wires between connectors: drawing the existing ones and dragging new ones.
    /// </summary>
    public partial class DesignerGrid
    {
        private const double SnapDistance = 16;

        private static readonly IBrush InvalidWireBrush = new ImmutableSolidColorBrush(Color.Parse("#E07A7A"));

        private readonly HashSet<Connector> _connectors = [];
        private WiresRenderer? _wiresRenderer;

        // Wire being dragged: from the fixed port to the pointer, or to the port it snaps to.
        private Connector? _dragPort;
        private Point _dragPoint;
        private Connector? _dragTarget;
        private string? _dragError;

        internal void RegisterConnector(Connector connector)
        {
            _ = _connectors.Add(connector);
            InvalidateWires();
        }

        internal void UnregisterConnector(Connector connector)
        {
            _ = _connectors.Remove(connector);
            InvalidateWires();
        }

        /// <summary>
        /// Redraws the wires, e.g. while a node is being dragged.
        /// </summary>
        public void InvalidateWires()
        {
            _wiresRenderer?.InvalidateVisual();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            MainViewModel.Connections.CollectionChanged += ConnectionsChanged;
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            MainViewModel.Connections.CollectionChanged -= ConnectionsChanged;
            LayoutUpdated -= OnLayoutUpdated;
        }

        private void ConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateWires();
        }

        // Nodes moving, growing or changing type all go through a layout pass.
        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            InvalidateWires();
        }

        private WiresRenderer EnsureWiresRenderer()
        {
            if (_wiresRenderer == null)
            {
                _wiresRenderer = new WiresRenderer(this) { ZIndex = -1, IsHitTestVisible = false };
                VisualChildren.Add(_wiresRenderer);
            }

            return _wiresRenderer;
        }

        #region Dragging a wire
        internal void BeginWire(Connector port, Point point)
        {
            // Grabbing a connected input picks up its wire, so it can be moved elsewhere or dropped.
            if (port.Parameter is Parameter parameter && MainViewModel.ConnectionTo(parameter) is Connection connection)
            {
                MainViewModel.Disconnect(connection);
                port = FindOutput(connection.SourceID) ?? port;
            }

            _dragPort = port;
            UpdateWire(point);
        }

        internal void UpdateWire(Point point)
        {
            if (_dragPort == null)
            {
                return;
            }

            _dragPoint = point;
            Connector? target = FindDropTarget(point);
            if (target != _dragTarget)
            {
                SetDropTarget(target);
            }

            InvalidateWires();
        }

        internal void EndWire(Point point)
        {
            if (_dragPort == null)
            {
                return;
            }

            UpdateWire(point);

            if (_dragTarget != null && _dragError == null && ResolveEnds(_dragPort, _dragTarget) is var (source, target, parameter))
            {
                _ = MainViewModel.Connect(source, target, parameter);
            }

            CancelWire();
        }

        internal void CancelWire()
        {
            SetDropTarget(null);
            _dragPort = null;
            InvalidateWires();
        }

        private void SetDropTarget(Connector? target)
        {
            if (_dragTarget != null)
            {
                _dragTarget.Classes.Remove("accept");
                _dragTarget.Classes.Remove("reject");
                ToolTip.SetIsOpen(_dragTarget, false);
                ToolTip.SetTip(_dragTarget, null);
            }

            _dragTarget = target;
            _dragError = null;

            if (target != null && _dragPort != null)
            {
                _dragError = ResolveEnds(_dragPort, target) is var (source, function, parameter)
                    ? Wiring.Validate(MainViewModel.Connections.Where(c => c.ParameterID != parameter.ID), source, function, parameter)
                    : "Only an output can be wired to an input.";

                target.Classes.Add(_dragError == null ? "accept" : "reject");
                if (_dragError != null)
                {
                    ToolTip.SetTip(target, _dragError);
                    ToolTip.SetIsOpen(target, true);
                }
            }
        }

        /// <summary>
        /// Nearest port of the opposite kind within snapping distance of <paramref name="point"/>.
        /// </summary>
        private Connector? FindDropTarget(Point point)
        {
            return _connectors
                .Where(c => c != _dragPort && c.IsInput != _dragPort!.IsInput)
                .Select(c => (Connector: c, Distance: Distance(c.CenterIn(this), point)))
                .Where(c => c.Distance <= SnapDistance)
                .OrderBy(c => c.Distance)
                .Select(c => c.Connector)
                .FirstOrDefault();
        }

        /// <summary>
        /// The output node, the receiving node and its parameter a wire between two ports would connect.
        /// </summary>
        private static (Model.Node Source, Model.Node Target, Parameter Parameter)? ResolveEnds(Connector a, Connector b)
        {
            (Connector output, Connector input) = a.IsInput ? (b, a) : (a, b);
            return output.Owner is Model.Node source && input.Owner is Model.Node target && input.Parameter is Parameter parameter
                ? (source, target, parameter)
                : null;
        }

        private Connector? FindOutput(string nodeID)
        {
            return _connectors.FirstOrDefault(c => !c.IsInput && c.Owner?.ID == nodeID);
        }

        private static double Distance(Point a, Point b)
        {
            Vector d = a - b;
            return Math.Sqrt((d.X * d.X) + (d.Y * d.Y));
        }
        #endregion

        /// <summary>
        /// Paints the wires over the grid lines and below the nodes.
        /// </summary>
        private sealed class WiresRenderer(DesignerGrid grid) : Control
        {
            private const double Thickness = 2.5;

            /// <summary>
            /// The node's color, adjusted to stand out from the canvas (a dark blue wire on the dark
            /// canvas would be nearly invisible).
            /// </summary>
            private IBrush WireBrush(Color color)
            {
                Color canvas = (grid.Background as ISolidColorBrush)?.Color ?? Colors.Black;
                return new SolidColorBrush(ColorContrast.StandOut(color, canvas));
            }

            public override void Render(DrawingContext context)
            {
                Dictionary<string, Connector> outputs = [];
                Dictionary<string, Connector> inputs = [];
                foreach (Connector connector in grid._connectors)
                {
                    if (connector.Parameter is Parameter parameter)
                    {
                        inputs[parameter.ID] = connector;
                    }
                    else if (connector.Owner is Model.Node node)
                    {
                        outputs[node.ID] = connector;
                    }
                }

                foreach (Connection connection in MainViewModel.Connections)
                {
                    if (outputs.TryGetValue(connection.SourceID, out Connector? output)
                        && inputs.TryGetValue(connection.ParameterID, out Connector? input)
                        && output.Owner is Model.Node source
                        && input.Parameter is Parameter parameter
                        && input.Owner is Model.Node target)
                    {
                        // A type changed after wiring (or a mapped function's parameters): keep the wire but flag it.
                        Pen pen = Wiring.Fits(source, target, parameter)
                            ? new Pen(WireBrush(source.Color), Thickness)
                            : new Pen(InvalidWireBrush, Thickness, new DashStyle([2, 2], 0));
                        DrawWire(context, pen, output.CenterIn(grid), input.CenterIn(grid));
                    }
                }

                if (grid._dragPort is Connector port)
                {
                    Point fixedEnd = port.CenterIn(grid);
                    Point looseEnd = grid._dragTarget != null && grid._dragError == null
                        ? grid._dragTarget.CenterIn(grid)
                        : grid._dragPoint;
                    IBrush brush = grid._dragError != null
                        ? InvalidWireBrush
                        : port.Owner is Model.Node node && !port.IsInput ? WireBrush(node.Color) : Brushes.Gray;
                    Pen pen = new(brush, Thickness, new DashStyle([2, 2], 0));

                    // Always draw from the output side so the curve leaves and enters horizontally.
                    if (port.IsInput)
                    {
                        DrawWire(context, pen, looseEnd, fixedEnd);
                    }
                    else
                    {
                        DrawWire(context, pen, fixedEnd, looseEnd);
                    }
                }
            }

            /// <summary>
            /// Bezier curve leaving <paramref name="from"/> to the right and entering <paramref name="to"/> from the left.
            /// </summary>
            private static void DrawWire(DrawingContext context, Pen pen, Point from, Point to)
            {
                double handle = Math.Max(40, Math.Abs(to.X - from.X) / 2);

                StreamGeometry geometry = new();
                using (StreamGeometryContext g = geometry.Open())
                {
                    g.BeginFigure(from, false);
                    g.CubicBezierTo(new Point(from.X + handle, from.Y), new Point(to.X - handle, to.Y), to);
                    g.EndFigure(false);
                }

                context.DrawGeometry(null, pen, geometry);
            }
        }
    }
}
