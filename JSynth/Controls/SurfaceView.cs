using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using JSynth.Runtime;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JSynth.Controls;

/// <summary>
/// A matrix drawn as a 3D surface (see <see cref="SurfaceMesh"/>): the squares of its grid coloured by height
/// with the heatmap's colour map, from the back to the front, a light wireframe over them, and the axes with
/// their ticks. Interactive, dragging turns it, the wheel zooms and a double-click goes back to the starting
/// view; it says which grid point the mouse is on (see <see cref="Pointing"/>).
/// </summary>
public sealed class SurfaceView : Avalonia.Controls.Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SurfaceView>();

    private static readonly ScottPlot.Colormaps.Viridis Colormap = new();

    private SurfaceMesh? _mesh;
    private string _title = string.Empty;
    private Point? _dragged;

    // As last drawn: where the grid points were and the order of the squares, for finding what is under the mouse.
    private (double X, double Y, double Depth)[,]? _points;
    private List<(int Row, int Column)> _order = [];
    private double _top;

    static SurfaceView()
    {
        AffectsRender<SurfaceView>(ForegroundProperty);
    }

    /// <summary>
    /// Small labels and fewer ticks, for a surface inside a node.
    /// </summary>
    public bool Compact { get; set; }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public SurfaceCamera Camera { get; private set; } = SurfaceCamera.Start;

    /// <summary>
    /// The grid point the mouse is on, (row, column) from 0; null off the surface.
    /// </summary>
    public (int Row, int Column)? Pointed { get; private set; }

    /// <summary>
    /// What is under the mouse, when that changes: the grid point's row, column and value; null off the surface.
    /// </summary>
    public event EventHandler<string?>? Pointing;

    /// <summary>
    /// Shows <paramref name="data"/> (a surface; nothing for null) from where it was looked at last: a surface
    /// drawn again and again (live, in a stream) keeps its view.
    /// </summary>
    public void Show(PlotData? data, string title)
    {
        _mesh = data is { Kind: PlotKind.Surface, Values: not null } ? new SurfaceMesh(data) : null;
        _title = title;
        Point(null);
        InvalidateVisual();
    }

    /// <summary>
    /// Back to the view the surface was first shown from.
    /// </summary>
    public void ResetView()
    {
        Camera = SurfaceCamera.Start;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // A background, so the mouse is seen everywhere on it.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        Color ink = (Foreground as ISolidColorBrush)?.Color ?? Colors.White;
        IBrush brush = new ImmutableSolidColorBrush(ink);

        _top = 0;
        if (!string.IsNullOrEmpty(_title))
        {
            FormattedText title = Text(_title, Compact ? 11 : 15, brush);
            title.MaxTextWidth = Math.Max(1, Bounds.Width - 12);
            title.MaxLineCount = 1;
            title.Trimming = TextTrimming.CharacterEllipsis;
            title.TextAlignment = TextAlignment.Center;
            context.DrawText(title, new Point(6, 4));
            _top = title.Height + 6;
        }

        if (_mesh is not SurfaceMesh mesh)
        {
            _points = null;
            return;
        }

        double width = Bounds.Width, height = Math.Max(1, Bounds.Height - _top);
        _points = mesh.Project(Camera, width, height);
        _order = mesh.Order(_points);

        using (context.PushTransform(Matrix.CreateTranslation(0, _top)))
        {
            context.Custom(new Squares(new Rect(0, 0, width, height), Triangles(mesh, _points, _order, ink)));
            Axes(context, mesh, width, height, ink, brush);

            if (Pointed is (int row, int column))
            {
                (double x, double y, _) = _points[row, column];
                context.DrawEllipse(null, new Pen(brush, 2), new Point(x, y), 6, 6);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragged = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_dragged is Point from)
        {
            // Across turns it around, up and down tilts it.
            Camera = Camera.Turned(-(position.X - from.X) * 0.4, (position.Y - from.Y) * 0.4);
            _dragged = position;
            InvalidateVisual();
        }

        Point(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragged = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragged = null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Point(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera = Camera.Zoomed(Math.Pow(1.15, e.Delta.Y));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        ResetView();
    }

    /// <summary>
    /// Finds the grid point at <paramref name="position"/> (on this control; none for null), marks it and says
    /// what it is.
    /// </summary>
    private void Point(Point? position)
    {
        (int, int)? pointed = position is Point p && _mesh is not null && _points is not null
            ? _mesh.PointAt(_points, _order, p.X, p.Y - _top)
            : null;
        if (pointed == Pointed)
        {
            return;
        }

        Pointed = pointed;
        InvalidateVisual();
        Pointing?.Invoke(this, pointed is (int row, int column) ? _mesh!.Describe(row, column) : null);
    }

    /// <summary>
    /// The squares as triangles, from the back to the front, coloured by height; after each, the lines of the
    /// wireframe it finishes (once both squares beside a line are drawn), so nearer squares cover them too.
    /// </summary>
    private (SKPoint[] Points, SKColor[] Colors) Triangles(SurfaceMesh mesh, (double X, double Y, double Depth)[,] points, List<(int Row, int Column)> order, Color ink)
    {
        List<SKPoint> vertices = new(order.Count * 12);
        List<SKColor> colors = new(order.Count * 12);
        SKColor[,] shades = new SKColor[mesh.Rows, mesh.Columns];
        for (int i = 0; i < mesh.Rows; i++)
        {
            for (int j = 0; j < mesh.Columns; j++)
            {
                ScottPlot.Color c = Colormap.GetColor(mesh.Fraction(mesh.Value(i, j)));
                shades[i, j] = new SKColor(c.R, c.G, c.B);
            }
        }

        // A line every few grid lines, about 24 across (12 in a node), and around the edges.
        int every = (int)Math.Max(1, Math.Ceiling(Math.Max(mesh.Rows, mesh.Columns) / (Compact ? 12.0 : 24.0)));
        bool Wired(int line, int count) => line % every == 0 || line == count - 1;
        SKColor wire = new(ink.R, ink.G, ink.B, 90);
        float thickness = Compact ? 0.8f : 1f;
        // Squares not drawn at all (with a value that isn't finite) count as drawn: their neighbours draw the line.
        bool[,] drawn = new bool[mesh.Rows, mesh.Columns];
        for (int i = 0; i < mesh.Rows; i++)
        {
            for (int j = 0; j < mesh.Columns; j++)
            {
                drawn[i, j] = true;
            }
        }

        foreach ((int i, int j) in order)
        {
            drawn[i, j] = false;
        }

        bool Drawn(int i, int j) => i < 0 || j < 0 || i + 1 >= mesh.Rows || j + 1 >= mesh.Columns || drawn[i, j];

        SKPoint At(int i, int j) => new((float)points[i, j].X, (float)points[i, j].Y);

        void Triangle((int, int) a, (int, int) b, (int, int) c)
        {
            foreach ((int i, int j) in new[] { a, b, c })
            {
                vertices.Add(At(i, j));
                colors.Add(shades[i, j]);
            }
        }

        void Line((int, int) a, (int, int) b)
        {
            SKPoint from = At(a.Item1, a.Item2), to = At(b.Item1, b.Item2);
            float dx = to.X - from.X, dy = to.Y - from.Y, length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.01f)
            {
                return;
            }

            SKPoint side = new(-dy / length * thickness / 2, dx / length * thickness / 2);
            vertices.AddRange([from + side, to + side, to - side, from + side, to - side, from - side]);
            colors.AddRange(Enumerable.Repeat(wire, 6));
        }

        foreach ((int i, int j) in order)
        {
            Triangle((i, j), (i, j + 1), (i + 1, j + 1));
            Triangle((i, j), (i + 1, j + 1), (i + 1, j));
            drawn[i, j] = true;

            // Each side of the square on a line of the wireframe, unless the square on its other side is still to come.
            if (Wired(i, mesh.Rows) && Drawn(i - 1, j))
            {
                Line((i, j), (i, j + 1));
            }

            if (Wired(i + 1, mesh.Rows) && Drawn(i + 1, j))
            {
                Line((i + 1, j), (i + 1, j + 1));
            }

            if (Wired(j, mesh.Columns) && Drawn(i, j - 1))
            {
                Line((i, j), (i + 1, j));
            }

            if (Wired(j + 1, mesh.Columns) && Drawn(i, j + 1))
            {
                Line((i, j + 1), (i + 1, j + 1));
            }
        }

        return ([.. vertices], [.. colors]);
    }

    /// <summary>
    /// The box's edges nearest the viewer below the surface, columns along one and rows along the other, and the
    /// values up its leftmost corner; each with its ticks and their numbers outside the box.
    /// </summary>
    private void Axes(DrawingContext context, SurfaceMesh mesh, double width, double height, Color ink, IBrush brush)
    {
        Pen pen = new(new ImmutableSolidColorBrush(ink, 0.7), 1);
        double bottom = -SurfaceMesh.Height;
        (double X, double Y, double Depth) Seen(double x, double y, double z) => Camera.Project(x, y, z, width, height);
        Point On((double X, double Y, double) p) => new(p.X, p.Y);

        // The front edges: nearer the viewer of the two sides.
        double front = Seen(0, -1, bottom).Depth >= Seen(0, 1, bottom).Depth ? -1 : 1;
        double side = Seen(-1, 0, bottom).Depth >= Seen(1, 0, bottom).Depth ? -1 : 1;
        (double x, double y) corner = new[] { (-1.0, -1.0), (-1.0, 1.0), (1.0, -1.0), (1.0, 1.0) }.MinBy(c => Seen(c.Item1, c.Item2, 0).X);

        double size = Compact ? 9 : 11;
        int count = Compact ? 3 : 6;

        void Axis((double, double, double) from, (double, double, double) to, IEnumerable<(double Value, (double, double, double) At, (double, double, double) Label)> ticks, string? name, (double, double, double) nameAt)
        {
            context.DrawLine(pen, On(Seen(from.Item1, from.Item2, from.Item3)), On(Seen(to.Item1, to.Item2, to.Item3)));
            foreach ((double value, (double x, double y, double z) at, (double x, double y, double z) label) in ticks)
            {
                Point start = On(Seen(at.x, at.y, at.z)), end = On(Seen(label.x, label.y, label.z));
                context.DrawLine(pen, start, start + ((end - start) * 0.35));
                FormattedText number = Text(PlotReadout.Number(value), size, brush);
                context.DrawText(number, end - new Point(number.Width / 2, number.Height / 2));
            }

            if (!Compact && name is not null)
            {
                FormattedText text = Text(name, size, brush);
                Point at = On(Seen(nameAt.Item1, nameAt.Item2, nameAt.Item3));
                context.DrawText(text, at - new Point(text.Width / 2, text.Height / 2));
            }
        }

        Axis((-1, front, bottom), (1, front, bottom),
            SurfaceMesh.Ticks(Middle(mesh.ColumnsOf(0)), Middle(mesh.ColumnsOf(mesh.Columns - 1)), count, whole: true)
                .Select(c => (c, (mesh.X(c), front, bottom), (mesh.X(c), front * 1.22, bottom))),
            "column", (0, front * 1.45, bottom));

        Axis((side, -1, bottom), (side, 1, bottom),
            SurfaceMesh.Ticks(Middle(mesh.RowsOf(0)), Middle(mesh.RowsOf(mesh.Rows - 1)), count, whole: true)
                .Select(r => (r, (side, mesh.Y(r), bottom), (side * 1.22, mesh.Y(r), bottom))),
            "row", (side * 1.45, 0, bottom));

        Axis((corner.x, corner.y, -SurfaceMesh.Height), (corner.x, corner.y, SurfaceMesh.Height),
            SurfaceMesh.Ticks(mesh.Min, mesh.Max, count, whole: false)
                .Select(v => (v, (corner.x, corner.y, mesh.Z(v)), (corner.x * 1.25, corner.y * 1.25, mesh.Z(v)))),
            null, default);
    }

    private static double Middle((long First, long Last) block) => (block.First + block.Last) / 2.0;

    private static FormattedText Text(string text, double size, IBrush brush)
    {
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
    }

    /// <summary>
    /// Draws the triangles with Skia, in one go, in their order: each covers those before it.
    /// </summary>
    private sealed class Squares(Rect bounds, (SKPoint[] Points, SKColor[] Colors) triangles) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (triangles.Points.Length == 0 || context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not ISkiaSharpApiLeaseFeature feature)
            {
                return;
            }

            using ISkiaSharpApiLease lease = feature.Lease();
            // The vertices' colours are multiplied by the paint's: white keeps them.
            using SKPaint paint = new() { Color = SKColors.White, IsAntialias = true };
            lease.SkCanvas.DrawVertices(SKVertexMode.Triangles, triangles.Points, null, triangles.Colors, paint);
        }
    }
}
