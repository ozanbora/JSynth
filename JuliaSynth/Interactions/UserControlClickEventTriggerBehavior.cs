using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace JuliaSynth.Interactions
{
    public class UserControlTriggerBehavior : Trigger<Control>
    {
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
            }
        }

        private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs eventArgs)
        {
            if (eventArgs.KeyModifiers == KeyModifiers)
            {
                _pointerPressedWhileCtrlPressed = true;
                _lastPoint = eventArgs.GetCurrentPoint(sender as Control);
            }
        }

        private void AssociatedObject_PointerMoved(object? sender, PointerEventArgs eventArgs)
        {
            if (eventArgs.KeyModifiers == KeyModifiers && _pointerPressedWhileCtrlPressed)
            {
                PointerPoint currentPoint = eventArgs.GetCurrentPoint(sender as Control);
                Vector offset = currentPoint.Position - _lastPoint.Position;

                _ = Interaction.ExecuteActions(AssociatedObject, Actions, parameter: offset);
                _lastPoint = currentPoint;
            }
        }

        private void AssociatedObject_PointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
        {
            if (_pointerPressedWhileCtrlPressed && eventArgs.KeyModifiers == KeyModifiers)
            {
                PointerPoint currentPoint = eventArgs.GetCurrentPoint(sender as Control);
                Vector offset = currentPoint.Position - _lastPoint.Position;

                _ = Interaction.ExecuteActions(AssociatedObject, Actions, parameter: offset);
            }

            // Always end the pan, even when the modifier was let go first.
            _pointerPressedWhileCtrlPressed = false;
        }
    }
}
