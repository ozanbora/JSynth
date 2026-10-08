using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Controls
{
    /// <summary>
    /// The design as a picture, to save as an image: its nodes and wires, without the grid lines or the selection.
    /// </summary>
    public partial class DesignerGrid
    {
        // Room around the nodes and wires.
        private const double PictureMargin = 24;

        /// <summary>
        /// Where the design is drawn on the canvas: around its nodes and wires; null when it has none.
        /// </summary>
        internal Rect? DrawnBounds()
        {
            IEnumerable<Rect> parts = this.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => (b.Classes.Contains("node") || b.Classes.Contains("frame")) && b.IsEffectivelyVisible)
                .Select(b => new Rect(b.TranslatePoint(default, this) ?? default, b.Bounds.Size))
                .Concat(Wires().Select(w => w.Geometry.Bounds));
            return parts.Any() ? parts.Aggregate((a, b) => a.Union(b)) : null;
        }

        /// <summary>
        /// The design drawn <paramref name="scale"/> times its size on the canvas at 100%, on the canvas's background;
        /// null when there is nothing to draw.
        /// </summary>
        public RenderTargetBitmap? Picture(double scale = 2)
        {
            if (DrawnBounds() is not Rect drawn)
            {
                return null;
            }

            Rect area = drawn.Inflate(PictureMargin);

            // Drawn as it is, but for the grid lines and the selection.
            HashSet<Model.Node> selected = [.. MainViewModel.Nodes.Where(n => n.IsSelected)];
            Connection? selectedWire = MainViewModel.SelectedConnection;
            MainViewModel.SelectOnly(null);
            bool lines = _gridLinesRenderer?.IsVisible ?? false;
            if (_gridLinesRenderer is not null)
            {
                _gridLinesRenderer.IsVisible = false;
            }

            try
            {
                RenderTargetBitmap bitmap = new(
                    new PixelSize((int)Math.Ceiling(area.Width * scale), (int)Math.Ceiling(area.Height * scale)),
                    new Vector(96 * scale, 96 * scale));
                using (DrawingContext context = bitmap.CreateDrawingContext())
                {
                    Rect page = new(area.Size);
                    context.FillRectangle(Background ?? Brushes.White, page);
                    context.FillRectangle(new VisualBrush(this)
                    {
                        SourceRect = new RelativeRect(area, RelativeUnit.Absolute),
                        DestinationRect = new RelativeRect(page, RelativeUnit.Absolute),
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                    }, page);
                }

                return bitmap;
            }
            finally
            {
                if (_gridLinesRenderer is not null)
                {
                    _gridLinesRenderer.IsVisible = lines;
                }

                MainViewModel.SelectedConnection = selectedWire;
                foreach (Model.Node node in selected)
                {
                    node.IsSelected = true;
                }
            }
        }
    }
}
