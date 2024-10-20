using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace JuliaSynth.Controls
{
    public partial class Node : UserControl
    {
        private bool _isPressed;
        private Point _positionInBlock;
        private Point _initialPosition;
        private TranslateTransform _transform = null!;
        private Control? ContentContainer = null;

        public Node() : base()
        {
            RenderOptions.SetRequiresFullOpacityHandling(this, true);
            ClipToBounds = false;
            Width = 180;
            Height = 120;

        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _isPressed = true;
            _positionInBlock = e.GetPosition((Visual?)Parent);
            _initialPosition = new Point(_positionInBlock.X, _positionInBlock.Y);
            if (_transform != null!)
            {
                _positionInBlock = new Point(
                    _positionInBlock.X - _transform.X,
                    _positionInBlock.Y - _transform.Y);
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _isPressed = false;

            // Snap to grid
            Point currentPosition = e.GetPosition((Visual?)Parent);
            double offsetX = currentPosition.X - _initialPosition.X;
            double offsetY = currentPosition.Y - _initialPosition.Y;


            ContentContainer = Parent as Control;

            if (ContentContainer is not null)
            {
                int currentRow = Grid.GetRow(ContentContainer);
                int currentColumn = Grid.GetColumn(ContentContainer);

                int nextRow = currentRow + ((int)offsetY / Constants.WIDTH);
                int nextColumn = currentColumn + ((int)offsetX / Constants.HEIGHT);

                nextRow = nextRow < 0 ? 0 : nextRow;
                nextColumn = nextColumn < 0 ? 0 : nextColumn;

                Grid.SetRow(ContentContainer, nextRow);
                Grid.SetColumn(ContentContainer, nextColumn);
            }

            base.OnPointerReleased(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (!_isPressed)
            {
                return;
            }

            if (Parent == null)
            {
                return;
            }

            Point currentPosition = e.GetPosition((Visual?)Parent);

            double offsetX = currentPosition.X - _positionInBlock.X;
            double offsetY = currentPosition.Y - _positionInBlock.Y;

            _transform = new TranslateTransform(offsetX, offsetY);
            RenderTransform = _transform;

            base.OnPointerMoved(e);

        }
    }
}
