using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace JuliaSynth.Interactions
{
    public class UserControlTriggerBehavior : Trigger<Control>
    {
        private KeyModifiers _savedKeyModifiers = KeyModifiers.None;

        private bool _pointerPressedWhileCtrlPressed = false;

        private PointerPoint _lastPoint;

        /// <summary>
        /// Identifies the <seealso cref="KeyModifiers"/> avalonia property.
        /// </summary>
        public static readonly StyledProperty<KeyModifiers> KeyModifiersProperty =
            AvaloniaProperty.Register<UserControlTriggerBehavior, KeyModifiers>(nameof(KeyModifiers));

        /// <summary>
        /// Gets or sets the required key modifiers to execute <see cref="Button.ClickEvent"/> event handler. This is a avalonia property.
        /// </summary>
        public KeyModifiers KeyModifiers
        {
            get => GetValue(KeyModifiersProperty);
            set => SetValue(KeyModifiersProperty, value);
        }

        /// <summary>
        /// Called after the behavior is attached to the <see cref="Behavior.AssociatedObject"/>.
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is { })
            {
                AssociatedObject.PointerPressed += AssociatedObject_PointerPressed;
                AssociatedObject.PointerMoved += AssociatedObject_PointerMoved;
                AssociatedObject.PointerReleased += AssociatedObject_PointerReleased;

                AssociatedObject.AddHandler(InputElement.KeyDownEvent, Button_OnKeyDown, RoutingStrategies.Tunnel);
                AssociatedObject.AddHandler(InputElement.KeyUpEvent, Button_OnKeyUp, RoutingStrategies.Tunnel);
            }
        }

        /// <summary>
        /// Called when the behavior is being detached from its <see cref="Behavior.AssociatedObject"/>.
        /// </summary>
        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject is { })
            {
                AssociatedObject.PointerPressed -= AssociatedObject_PointerPressed;
                AssociatedObject.PointerMoved -= AssociatedObject_PointerMoved;
                AssociatedObject.PointerReleased -= AssociatedObject_PointerReleased;

                AssociatedObject.RemoveHandler(InputElement.KeyDownEvent, Button_OnKeyDown);
                AssociatedObject.RemoveHandler(InputElement.KeyUpEvent, Button_OnKeyUp);
            }
        }

        private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
        {
            if (KeyModifiers == _savedKeyModifiers)
            {
                _pointerPressedWhileCtrlPressed = true;
                _lastPoint = eventArgs.GetCurrentPoint(sender as Control);
            }
        }

        private void AssociatedObject_PointerMoved(object? sender, PointerEventArgs eventArgs)
        {
            if (KeyModifiers == _savedKeyModifiers && _pointerPressedWhileCtrlPressed)
            {
                PointerPoint currentPoint = eventArgs.GetCurrentPoint(sender as Control);
                Vector offset = currentPoint.Position - _lastPoint.Position;

                _ = Interaction.ExecuteActions(AssociatedObject, Actions, parameter: offset);
                _lastPoint = currentPoint;
            }
        }

        private void AssociatedObject_PointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
        {
            if (KeyModifiers == _savedKeyModifiers)
            {
                if (_pointerPressedWhileCtrlPressed)
                {
                    PointerPoint currentPoint = eventArgs.GetCurrentPoint(sender as Control);
                    Vector offset = currentPoint.Position - _lastPoint.Position;

                    _ = Interaction.ExecuteActions(AssociatedObject, Actions, parameter: offset);
                }

                _pointerPressedWhileCtrlPressed = false;
            }
        }

        private void Button_OnKeyDown(object? sender, KeyEventArgs e)
        {
            _savedKeyModifiers = e.KeyModifiers;
        }

        private void Button_OnKeyUp(object? sender, KeyEventArgs e)
        {
            _savedKeyModifiers = KeyModifiers.None;
        }
    }
}
