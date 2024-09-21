using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace JuliaSynth.Interactions
{
    public class UserControlMouseWheelEventTriggerBehavior : Trigger<Control>
    {
        private readonly KeyModifiers _savedKeyModifiers = KeyModifiers.None;

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
                AssociatedObject.PointerWheelChanged += AssociatedObject_PointerWheelChanged;

                //AssociatedObject.AddHandler(InputElement.KeyDownEvent, Button_OnKeyDown, RoutingStrategies.Tunnel);
                //AssociatedObject.AddHandler(InputElement.KeyUpEvent, Button_OnKeyUp, RoutingStrategies.Tunnel);
            }
        }

        private void AssociatedObject_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers)
            {
                _ = Interaction.ExecuteActions(AssociatedObject, Actions, parameter: e.Delta);
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
                AssociatedObject.PointerWheelChanged -= AssociatedObject_PointerWheelChanged;

                //AssociatedObject.RemoveHandler(InputElement.KeyDownEvent, Button_OnKeyDown);
                //AssociatedObject.RemoveHandler(InputElement.KeyUpEvent, Button_OnKeyUp);
            }
        }

        //private void Button_OnKeyDown(object? sender, KeyEventArgs e)
        //{
        //    _savedKeyModifiers = e.KeyModifiers;
        //}

        //private void Button_OnKeyUp(object? sender, KeyEventArgs e)
        //{
        //    _savedKeyModifiers = KeyModifiers.None;
        //}
    }
}
