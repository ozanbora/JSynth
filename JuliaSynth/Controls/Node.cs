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

            Grid.SetRow(this, Grid.GetRow(this) + ((int)offsetY / 20));
            Grid.SetColumn(this, Grid.GetColumn(this) + ((int)offsetX / 20));

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
