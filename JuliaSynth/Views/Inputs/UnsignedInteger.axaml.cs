using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace JuliaSynth.Views.Inputs;

public partial class UnsignedInteger : UserControl
{
    public UnsignedInteger()
    {
        InitializeComponent();
    }

    private bool _isPressed;
    private Point _positionInBlock;
    private TranslateTransform _transform = null!;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _isPressed = true;
        _positionInBlock = e.GetPosition((Avalonia.Visual?)Parent);

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

        Avalonia.Point currentPosition = e.GetPosition((Avalonia.Visual?)Parent);

        double offsetX = currentPosition.X - _positionInBlock.X;
        double offsetY = currentPosition.Y - _positionInBlock.Y;

        _transform = new TranslateTransform(offsetX, offsetY);
        RenderTransform = _transform;

        base.OnPointerMoved(e);
    }
}