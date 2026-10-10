using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using JSynth.Converters;
using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace JSynth.Controls
{
    /// <summary>
    /// Wires between connectors: drawing the existing ones and dragging new ones.
    /// </summary>
    public partial class DesignerGrid
    {
        private const double SnapDistance = 16;

        // How wide a wire is to a click: a few pixels either side of it.
        private const double WireReach = 10;

        // How far below its ports a wire going back runs: more than a node's height, so it passes under them.
        private const double BackWireDrop = 110;

        // A breakpoint's dot, and how near a click on it must be.
        private const double BreakpointRadius = 7;

        private static readonly IBrush BreakpointBrush = new ImmutableSolidColorBrush(Color.Parse("#CB3C33"));
        private static readonly IPen PausedGlow = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#CB3C33"), 0.55), 12);

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
            MainViewModel.SelectionChanged += InvalidateWires;
            MainViewModel.BreakpointsChanged += InvalidateWires;
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            MainViewModel.Connections.CollectionChanged -= ConnectionsChanged;
            MainViewModel.SelectionChanged -= InvalidateWires;
            MainViewModel.BreakpointsChanged -= InvalidateWires;
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
                port = FindOutput(connection.SourceID, connection.Output) ?? port;
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

            if (_dragTarget != null && _dragError == null && ResolveEnds(_dragPort, _dragTarget) is var (source, target, parameter, output))
            {
                _ = MainViewModel.Connect(source, target, parameter, output);
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
                _dragError = ResolveEnds(_dragPort, target) is var (source, function, parameter, output)
                    ? Wiring.Validate(MainViewModel.Nodes, MainViewModel.Connections.Where(c => c.ParameterID != parameter.ID), source, function, parameter, output)
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
        /// The output node, the receiving node, its parameter and the output (null: the node's value) a wire between two
        /// ports would connect.
        /// </summary>
        private static (Model.Node Source, Model.Node Target, Parameter Parameter, string? Output)? ResolveEnds(Connector a, Connector b)
        {
            (Connector output, Connector input) = a.IsInput ? (b, a) : (a, b);
            return output.Owner is Model.Node source && input.Owner is Model.Node target && input.Parameter is Parameter parameter
                ? (source, target, parameter, output.Output)
                : null;
        }

        private Connector? FindOutput(string nodeID, string? output)
        {
            return _connectors.FirstOrDefault(c => !c.IsInput && c.Owner?.ID == nodeID && c.Output == output);
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
            private const double HighlightThickness = 4;
            private const double FadedOpacity = 0.3;

            private static readonly IPen Glow = new ImmutablePen(new ImmutableSolidColorBrush(SelectionColor, 0.6), 10);

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
                // The selected wire and those of the selected nodes stand out: drawn last, bolder, with a glow of the
                // blue of a selected node's border. With a wire selected, the others fade.
                bool wireSelected = MainViewModel.SelectedConnection is not null;
                List<Wire> wires = [.. grid.Wires().OrderBy(w => w.Highlighted)];
                foreach (Wire wire in wires)
                {
                    // The wire a run is paused at glows red.
                    if (wire.Connection == MainViewModel.PausedWire)
                    {
                        context.DrawGeometry(null, PausedGlow, wire.Geometry);
                    }


                    // A type changed after wiring (or a mapped function's parameters): keep the wire but flag it.
                    // A wire into a next port carries the value on to the next file: dashed, as it goes back.
                    IBrush brush = wire.Fits ? WireBrush(wire.Source.Color) : InvalidWireBrush;
                    DashStyle? dashes = !wire.Fits ? new DashStyle([2, 2], 0) : wire.Back ? new DashStyle([3, 1.5], 0) : null;
                    if (wire.Highlighted)
                    {
                        context.DrawGeometry(null, Glow, wire.Geometry);
                        context.DrawGeometry(null, new Pen(brush, HighlightThickness, dashes), wire.Geometry);
                    }
                    else
                    {
                        using (context.PushOpacity(wireSelected ? FadedOpacity : 1))
                        {
                            context.DrawGeometry(null, new Pen(brush, Thickness, dashes), wire.Geometry);
                        }
                    }
                }

                // Breakpoints: a red dot halfway along the wire; a ring when off.
                foreach (Wire wire in wires.Where(w => w.Connection.Breakpoint is not null))
                {
                    bool on = wire.Connection.Breakpoint!.On;
                    context.DrawEllipse(on ? BreakpointBrush : grid.Background, new Pen(on ? Brushes.White : BreakpointBrush, 2), wire.Middle, BreakpointRadius, BreakpointRadius);
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
                    context.DrawGeometry(null, pen, port.IsInput ? WireGeometry(looseEnd, fixedEnd) : WireGeometry(fixedEnd, looseEnd));
                }
            }
        }

        /// <summary>
        /// A wire as drawn: its connection and nodes, whether its types fit, whether it goes back to a next port, whether
        /// it stands out (selected, or a selected node's), its path, and its middle, where a breakpoint is drawn.
        /// </summary>
        private sealed record Wire(Connection Connection, Model.Node Source, bool Fits, bool Back, bool Highlighted, Geometry Geometry, Point Middle);

        /// <summary>
        /// The wires between the connectors on the canvas.
        /// </summary>
        private IEnumerable<Wire> Wires()
        {
            Dictionary<string, Connector> outputs = [];
            Dictionary<string, Connector> inputs = [];
            foreach (Connector connector in _connectors)
            {
                if (connector.Parameter is Parameter parameter)
                {
                    inputs[parameter.ID] = connector;
                }
                else if (connector.Owner is Model.Node node)
                {
                    outputs[$"{node.ID}/{connector.Output}"] = connector;
                }
            }

            Connection? selected = MainViewModel.SelectedConnection;
            foreach (Connection connection in MainViewModel.Connections)
            {
                if (outputs.TryGetValue($"{connection.SourceID}/{connection.Output}", out Connector? output)
                    && inputs.TryGetValue(connection.ParameterID, out Connector? input)
                    && output.Owner is Model.Node source
                    && input.Parameter is Parameter parameter
                    && input.Owner is Model.Node target)
                {
                    bool back = parameter == target.Next;
                    Point from = output.CenterIn(this);
                    Point to = input.CenterIn(this);
                    yield return new Wire(
                        connection,
                        source,
                        Wiring.Fits(source, target, parameter, connection.Output),
                        back,
                        connection == selected || source.IsSelected || target.IsSelected,
                        back ? BackWireGeometry(from, to) : WireGeometry(from, to),
                        back ? new Point((from.X + to.X) / 2, Math.Max(from.Y, to.Y) + BackWireDrop) : new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
                }
            }
        }

        /// <summary>
        /// The wire whose breakpoint's dot is at <paramref name="point"/>; null when none is.
        /// </summary>
        internal Connection? BreakpointAt(Point point)
        {
            return Wires().FirstOrDefault(w => w.Connection.Breakpoint is not null && Distance(w.Middle, point) <= BreakpointRadius + 3)?.Connection;
        }

        /// <summary>
        /// Opens the card setting <paramref name="wire"/>'s breakpoint, at its dot.
        /// </summary>
        internal void EditBreakpoint(Connection wire)
        {
            Flyout flyout = new() { Placement = PlacementMode.Pointer, ShowMode = FlyoutShowMode.Transient };
            flyout.Content = new BreakpointEditor { DataContext = new BreakpointEditing(wire, flyout.Hide) };
            flyout.ShowAt(this, showAtPointer: true);
        }

        /// <summary>
        /// The wire passing within reach of <paramref name="point"/>, the nearest drawn on top; null when none does.
        /// </summary>
        internal Connection? WireAt(Point point)
        {
            Pen reach = new(Brushes.Black, WireReach);
            return Wires().OrderBy(w => w.Highlighted).LastOrDefault(w => w.Geometry.StrokeContains(reach, point))?.Connection;
        }

        #region Peeking at a wire
        // Lines of a value shown over a wire: enough for a small matrix.
        private const int PeekLines = 16;

        private Connection? _peeked;

        /// <summary>
        /// With the pointer over a wire on the empty canvas (<paramref name="point"/>; null: elsewhere), shows what it
        /// carries: the value the last run gave, as a node's badge does.
        /// </summary>
        private void PeekWire(Point? point)
        {
            Connection? wire = point is Point at && _dragPort is null ? WireAt(at) : null;
            if (wire == _peeked)
            {
                return;
            }

            _peeked = wire;
            ToolTip.SetIsOpen(this, false);
            ToolTip.SetTip(this, wire is null ? null : new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                FontSize = 12,
                MaxWidth = 520,
                TextWrapping = TextWrapping.Wrap,
                Text = WhatWireCarries(wire),
            });
            ToolTip.SetIsOpen(this, wire is not null);
        }

        /// <summary>
        /// The type of the value on <paramref name="wire"/> and, after a run, the value (its first lines).
        /// </summary>
        internal static string WhatWireCarries(Connection wire)
        {
            if (MainViewModel.Nodes.FirstOrDefault(n => n.ID == wire.SourceID) is not Model.Node source)
            {
                return string.Empty;
            }

            string type = source.OutputTypeOf(wire.Output);
            if (wire.Output is not null)
            {
                return $"{type}\nThe {wire.Output} of each file, as it is read";
            }

            if (source.Result is null)
            {
                return $"{type}\nRun the design (F5) to see the value";
            }

            string[] lines = source.ResultTip.Split('\n');
            return lines.Length <= PeekLines ? source.ResultTip : string.Join('\n', lines.Take(PeekLines).Append("⋮"));
        }
        #endregion

        /// <summary>
        /// A wire carrying a value back to an input for the next file: out to the right of
        /// <paramref name="from"/>, along under both nodes, and into <paramref name="to"/> from the left.
        /// </summary>
        private static StreamGeometry BackWireGeometry(Point from, Point to)
        {
            // Below the ports by more than a node's height, so the wire passes under them.
            double below = Math.Max(from.Y, to.Y) + BackWireDrop;
            const double Turn = 30;

            StreamGeometry geometry = new();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(from, false);
                g.CubicBezierTo(new Point(from.X + (2 * Turn), from.Y), new Point(from.X + (2 * Turn), below), new Point(from.X, below));
                g.LineTo(new Point(to.X, below));
                g.CubicBezierTo(new Point(to.X - (2 * Turn), below), new Point(to.X - (2 * Turn), to.Y), to);
                g.EndFigure(false);
            }

            return geometry;
        }

        /// <summary>
        /// Bezier curve leaving <paramref name="from"/> to the right and entering <paramref name="to"/> from the left.
        /// </summary>
        private static StreamGeometry WireGeometry(Point from, Point to)
        {
            double handle = Math.Max(40, Math.Abs(to.X - from.X) / 2);

            StreamGeometry geometry = new();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.BeginFigure(from, false);
                g.CubicBezierTo(new Point(from.X + handle, from.Y), new Point(to.X - handle, to.Y), to);
                g.EndFigure(false);
            }

            return geometry;
        }
    }
}
