using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace JuliaSynth.Controls
{
    public class DesignerGrid : Grid
    {
        private readonly int NumberOfRows = Constants.ROWS;
        private readonly int NumberOfColumns = Constants.COLUMNS;

        private new readonly GridLength Height = new(Constants.HEIGHT);
        private new readonly GridLength Width = new(Constants.WIDTH);

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
            Border.BorderBrushProperty.AddOwner<Grid>();

        public double GridLineThickness
        {
            get => (double)GetValue(GridLineThicknessProperty);
            set => SetValue(GridLineThicknessProperty, value);
        }

        public static readonly StyledProperty<double> GridLineThicknessProperty =
            AvaloniaProperty.Register<DesignerGrid, double>(nameof(GridLineThickness), 1.0);
        #endregion

        public DesignerGrid() : base()
        {
            RowDefinitions = []; ColumnDefinitions = [];

            for (int i = 0; i < NumberOfRows; i++)
            {
                RowDefinitions.Add(new RowDefinition() { Height = Height });
            }

            for (int i = 0; i < NumberOfColumns; i++)
            {
                ColumnDefinitions.Add(new ColumnDefinition() { Width = Width });
            }

            MinWidth = NumberOfColumns * Width.Value;
            MaxHeight = NumberOfRows * Height.Value;
            RenderOptions.SetRequiresFullOpacityHandling(this, true);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            //  update render bound on grid lines renderer visual
            GridLinesRenderer? gridLinesRenderer = EnsureGridLinesRenderer();
            gridLinesRenderer?.UpdateRenderBounds(arrangeSize);

            return base.ArrangeOverride(arrangeSize);
        }

        private GridLinesRenderer? EnsureGridLinesRenderer()
        {
            //
            //  synchronize the state
            //
            if (ShowCustomGridLines && (_gridLinesRenderer == null))
            {
                _gridLinesRenderer = new GridLinesRenderer(
                    GridLineBrush, GridLineThickness);
                VisualChildren.Add(_gridLinesRenderer);
            }

            if ((!ShowCustomGridLines) && (_gridLinesRenderer != null))
            {
                _ = VisualChildren.Remove(_gridLinesRenderer);
                _gridLinesRenderer = null;
            }

            return _gridLinesRenderer;
        }

        /// <summary>
        /// Helper for rendering grid lines.
        /// </summary>
        internal class GridLinesRenderer : Control
        {
            /// <summary>
            /// Static initialization
            /// </summary>
            internal GridLinesRenderer(IBrush? brush, double thickness)
            {
                List<double> dashArray = [_dashLength, _dashLength];

                DashStyle ds1 = new(dashArray, 0);
                _dashPen = new Pen(brush, thickness, lineCap: PenLineCap.Flat, dashStyle: ds1);
            }

            /// <summary>
            /// UpdateRenderBounds.
            /// </summary>
            public sealed override void Render(DrawingContext drawingContext)
            {
                DesignerGrid? grid = this.GetVisualParent<DesignerGrid>();

                if (grid == null || !grid.ShowCustomGridLines)
                {
                    return;
                }

                for (int i = 1; i < grid.ColumnDefinitions.Count; ++i)
                {
                    ColumnDefinition column = grid.ColumnDefinitions[i];
                    double finalOffset = Convert.ToDouble(column.GetType()
                        .GetProperty("FinalOffset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(column));
                    DrawGridLine(
                        drawingContext,
                        finalOffset, 0.0,
                        finalOffset, _lastArrangeSize.Height);
                }

                for (int i = 1; i < grid.RowDefinitions.Count; ++i)
                {
                    RowDefinition row = grid.RowDefinitions[i];
                    double finalOffset = Convert.ToDouble(row.GetType()
                        .GetProperty("FinalOffset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(row));
                    DrawGridLine(
                        drawingContext,
                        0.0, finalOffset,
                        _lastArrangeSize.Width, finalOffset);
                }
            }

            /// <summary>
            /// Draw single hi-contrast line.
            /// </summary>
            private void DrawGridLine(
                DrawingContext drawingContext,
                double startX,
                double startY,
                double endX,
                double endY)
            {
                Point start = new(startX, startY);
                Point end = new(endX, endY);
                drawingContext.DrawLine(_dashPen, start, end);
            }

            internal void UpdateRenderBounds(Size arrangeSize)
            {
                _lastArrangeSize = arrangeSize;
                InvalidateVisual();
            }

            private Size _lastArrangeSize;
            private const double _dashLength = 4.0;
            private readonly Pen _dashPen;
        }
    }
}