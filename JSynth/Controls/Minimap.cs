using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace JSynth.Controls
{
    /// <summary>
    /// The whole design in small, with the part of it in view outlined: a click or a drag on it brings that part into
    /// view.
    /// </summary>
    public class Minimap : Control
    {
        // Canvas left around the design, in pixels of the canvas at 100%.
        private const double Room = 200;

        private static readonly IBrush ViewFill = new ImmutableSolidColorBrush(Color.Parse("#9558B2"), 0.15);
        private static readonly IPen ViewPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#9558B2")), 1.5);
        private static readonly IPen SelectedPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#9558B2")), 1.5);

        public static readonly StyledProperty<Vector> OffsetProperty =
            AvaloniaProperty.Register<Minimap, Vector>(nameof(Offset), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<Size> ViewportSizeProperty =
            AvaloniaProperty.Register<Minimap, Size>(nameof(ViewportSize));

        public static readonly StyledProperty<double> ScaleProperty =
            AvaloniaProperty.Register<Minimap, double>(nameof(Scale), 1.0);

        /// <summary>
        /// Where the canvas is scrolled to, in pixels on screen.
        /// </summary>
        public Vector Offset
        {
            get => GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }

        /// <summary>
        /// How much of the canvas is in view, in pixels on screen.
        /// </summary>
        public Size ViewportSize
        {
            get => GetValue(ViewportSizeProperty);
            set => SetValue(ViewportSizeProperty, value);
        }

        /// <summary>
        /// How large the canvas is drawn: screen pixels per pixel of the canvas.
        /// </summary>
        public double Scale
        {
            get => GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        // While dragging, the map stays as it was when the drag started, rather than following the view it moves.
        private (Rect Area, double Factor, Point Origin)? _heldMap;

        static Minimap()
        {
            AffectsRender<Minimap>(OffsetProperty, ViewportSizeProperty, ScaleProperty);
        }

        public Minimap()
        {
            ClipToBounds = true;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            MainViewModel.Nodes.CollectionChanged += NodesChanged;
            Track(MainViewModel.Nodes, true);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            MainViewModel.Nodes.CollectionChanged -= NodesChanged;
            Track(MainViewModel.Nodes, false);
        }

        private void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Track(e.OldItems?.OfType<Model.Node>() ?? [], false);
            Track(e.NewItems?.OfType<Model.Node>() ?? [], true);
            InvalidateVisual();
        }

        private void Track(System.Collections.Generic.IEnumerable<Model.Node> nodes, bool track)
        {
            foreach (Model.Node node in nodes)
            {
                if (track)
                {
                    node.PropertyChanged += NodeChanged;
                }
                else
                {
                    node.PropertyChanged -= NodeChanged;
                }
            }
        }

        private void NodeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Model.Node.Row) or nameof(Model.Node.Column) or nameof(Model.Node.Color) or nameof(Model.Node.IsSelected)
                or nameof(FrameNode.Columns) or nameof(FrameNode.Rows))
            {
                InvalidateVisual();
            }
        }

        /// <summary>
        /// The part of the canvas in view, in pixels of the canvas at 100%.
        /// </summary>
        private Rect View => Scale <= 0 ? default : new Rect(Offset.X / Scale, Offset.Y / Scale, ViewportSize.Width / Scale, ViewportSize.Height / Scale);

        private static Rect Footprint(Model.Node node)
        {
            (double top, double left, double bottom, double right) = Layout.Footprint(node);
            return new Rect(left * Constants.WIDTH, top * Constants.HEIGHT, (right - left) * Constants.WIDTH, (bottom - top) * Constants.HEIGHT);
        }

        /// <summary>
        /// What the map shows, in pixels of the canvas at 100%: the design with room around it, and the view; how many
        /// map pixels a canvas pixel takes; and where the area's corner is drawn.
        /// </summary>
        internal (Rect Area, double Factor, Point Origin) Map()
        {
            if (_heldMap is { } held)
            {
                return held;
            }

            Rect area = MainViewModel.Nodes.Count == 0
                ? View
                : MainViewModel.Nodes.Select(Footprint).Aggregate((a, b) => a.Union(b)).Inflate(Room).Union(View);
            double factor = area.Width <= 0 || area.Height <= 0 ? 1 : Math.Min(Bounds.Width / area.Width, Bounds.Height / area.Height);
            Point origin = new((Bounds.Width - (area.Width * factor)) / 2, (Bounds.Height - (area.Height * factor)) / 2);
            return (area, factor, origin);
        }

        public override void Render(DrawingContext context)
        {
            (Rect area, double factor, Point origin) = Map();
            Rect OnMap(Rect r) => new(origin.X + ((r.X - area.X) * factor), origin.Y + ((r.Y - area.Y) * factor), r.Width * factor, r.Height * factor);

            // Frames behind, faint; the nodes in their colours, the selected outlined.
            foreach (FrameNode frame in MainViewModel.Nodes.OfType<FrameNode>())
            {
                context.DrawRectangle(new SolidColorBrush(frame.Color, 0.3), null, OnMap(Footprint(frame)), 2, 2);
            }

            foreach (Model.Node node in MainViewModel.Nodes.Where(n => n is not FrameNode))
            {
                context.DrawRectangle(new SolidColorBrush(node.Color), node.IsSelected ? SelectedPen : null, OnMap(Footprint(node)), 1.5, 1.5);
            }

            context.DrawRectangle(ViewFill, ViewPen, OnMap(View));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _heldMap = Map();
            e.Pointer.Capture(this);
            ShowAround(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_heldMap is not null)
            {
                ShowAround(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_heldMap is not null)
            {
                _heldMap = null;
                e.Pointer.Capture(null);
                InvalidateVisual();
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _heldMap = null;
        }

        /// <summary>
        /// Scrolls the canvas so the place at <paramref name="point"/> on the map is in the middle of the view.
        /// </summary>
        private void ShowAround(Point point)
        {
            (Rect area, double factor, Point origin) = Map();
            if (factor <= 0)
            {
                return;
            }

            Point onCanvas = new(area.X + ((point.X - origin.X) / factor), area.Y + ((point.Y - origin.Y) / factor));
            Offset = new Vector(
                Math.Max(0, (onCanvas.X * Scale) - (ViewportSize.Width / 2)),
                Math.Max(0, (onCanvas.Y * Scale) - (ViewportSize.Height / 2)));
        }
    }
}
