using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace JuliaSynth.Controls
{
    /// <summary>
    /// Designer surface made of <see cref="Constants.ROWS"/> x <see cref="Constants.COLUMNS"/> fixed-size cells.
    /// Children are placed in a cell through the Grid.Row/Grid.Column attached properties.
    /// </summary>
    /// <remarks>
    /// This is deliberately not a <see cref="Grid"/>: with hundreds of row and column definitions every
    /// layout pass of a Grid is expensive, while fixed-size cells only need a multiplication.
    /// </remarks>
    public partial class DesignerGrid : Panel
    {
        private const double DashLength = 4.0;

        private static readonly double CellHeight = Constants.HEIGHT;
        private static readonly double CellWidth = Constants.WIDTH;

        private IBrush? _gridLinesBrush;
        private GridLinesRenderer? _gridLinesRenderer;

        #region Properties
        public bool ShowCustomGridLines
        {
            get => GetValue(ShowCustomGridLinesProperty);
            set => SetValue(ShowCustomGridLinesProperty, value);
        }

        public static readonly StyledProperty<bool> ShowCustomGridLinesProperty =
            AvaloniaProperty.Register<DesignerGrid, bool>(nameof(ShowCustomGridLines), false);

        public IBrush? GridLineBrush
        {
            get => GetValue(GridLineBrushProperty);
            set => SetValue(GridLineBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
            Border.BorderBrushProperty.AddOwner<DesignerGrid>();

        public double GridLineThickness
        {
            get => GetValue(GridLineThicknessProperty);
            set => SetValue(GridLineThicknessProperty, value);
        }

        public static readonly StyledProperty<double> GridLineThicknessProperty =
            AvaloniaProperty.Register<DesignerGrid, double>(nameof(GridLineThickness), 1.0);
        #endregion

        static DesignerGrid()
        {
            AffectsParentArrange<DesignerGrid>(Grid.RowProperty, Grid.ColumnProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GridLineBrushProperty || change.Property == GridLineThicknessProperty)
            {
                _gridLinesBrush = null;
                _gridLinesRenderer?.InvalidateVisual();
            }
            else if (change.Property == ShowCustomGridLinesProperty)
            {
                InvalidateArrange();
            }
            else if (change.Property == BackgroundProperty)
            {
                // Wire colors are adjusted to the background.
                InvalidateWires();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Measure like a Grid with fixed-size cells: each child gets exactly one cell.
            Size cell = new(CellWidth, CellHeight);
            foreach (Control child in Children)
            {
                child.Measure(cell);
            }

            return new Size(Constants.COLUMNS * CellWidth, Constants.ROWS * CellHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            EnsureGridLinesRenderer()?.Arrange(new Rect(finalSize));
            EnsureWiresRenderer().Arrange(new Rect(finalSize));
            EnsureSelectionRenderer().Arrange(new Rect(finalSize));

            foreach (Control child in Children)
            {
                int row = Math.Clamp(Grid.GetRow(child), 0, Constants.ROWS - 1);
                int column = Math.Clamp(Grid.GetColumn(child), 0, Constants.COLUMNS - 1);
                child.Arrange(new Rect(column * CellWidth, row * CellHeight, CellWidth, CellHeight));
            }

            return finalSize;
        }

        private GridLinesRenderer? EnsureGridLinesRenderer()
        {
            if (ShowCustomGridLines && _gridLinesRenderer == null)
            {
                // Below the frames and the wires, which are below the nodes.
                _gridLinesRenderer = new GridLinesRenderer(this) { ZIndex = -3 };
                VisualChildren.Add(_gridLinesRenderer);
            }

            if (!ShowCustomGridLines && _gridLinesRenderer != null)
            {
                _ = VisualChildren.Remove(_gridLinesRenderer);
                _gridLinesRenderer = null;
            }

            return _gridLinesRenderer;
        }

        /// <summary>
        /// Builds a brush holding the dashed lines of a few cells, tiled over the whole surface.
        /// Drawing hundreds of dashed lines spanning the whole surface is far too slow.
        /// </summary>
        private IBrush CreateGridLinesBrush()
        {
            double thickness = GridLineThickness;

            // Pen dashes are scaled by the thickness; the tile has to hold a whole number of dash
            // periods, otherwise the dashes would not line up from one tile to the next.
            double dashPeriod = 2 * DashLength * thickness;
            int cells = 1;
            while (cells < 16 && !(IsMultipleOf(cells * CellWidth, dashPeriod) && IsMultipleOf(cells * CellHeight, dashPeriod)))
            {
                cells++;
            }

            double tileWidth = cells * CellWidth;
            double tileHeight = cells * CellHeight;

            // Lines run through the middle of the tile's cells so they are not clipped by the tile
            // edges; the tile is shifted back by half a cell below so they land on cell boundaries.
            GeometryGroup lines = new();
            for (int i = 0; i < cells; i++)
            {
                double x = (i + 0.5) * CellWidth;
                double y = (i + 0.5) * CellHeight;
                lines.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, tileHeight)));
                lines.Children.Add(new LineGeometry(new Point(0, y), new Point(tileWidth, y)));
            }

            Rect tile = new(0, 0, tileWidth, tileHeight);
            DrawingGroup drawing = new()
            {
                Children =
                {
                    // Transparent fill so that the drawing's bounds are exactly one tile.
                    new GeometryDrawing { Geometry = new RectangleGeometry(tile), Brush = Brushes.Transparent },
                    new GeometryDrawing
                    {
                        Geometry = lines,
                        Pen = new Pen(GridLineBrush, thickness, new DashStyle([DashLength, DashLength], 0), PenLineCap.Flat),
                    },
                },
            };

            return new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(-CellWidth / 2, -CellHeight / 2, tileWidth, tileHeight, RelativeUnit.Absolute),
            };
        }

        private static bool IsMultipleOf(double value, double period)
        {
            double remainder = value % period;
            return remainder < 1e-6 || period - remainder < 1e-6;
        }

        /// <summary>
        /// Paints the grid lines over the whole surface, below the nodes.
        /// </summary>
        private sealed class GridLinesRenderer(DesignerGrid grid) : Control
        {
            public override void Render(DrawingContext context)
            {
                grid._gridLinesBrush ??= grid.CreateGridLinesBrush();

                // Only the lines between cells, not the ones on the outer edges.
                Rect inner = new Rect(Bounds.Size).Deflate(new Thickness(CellWidth / 2, CellHeight / 2));
                using (context.PushClip(inner))
                {
                    context.FillRectangle(grid._gridLinesBrush, new Rect(Bounds.Size));
                }
            }
        }
    }
}
