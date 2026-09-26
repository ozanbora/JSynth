using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Linq;
using System.Threading.Tasks;

namespace JuliaSynth.Views;

/// <summary>
/// A small modal question with a few buttons; Avalonia has no built-in message box.
/// </summary>
public sealed class MessageDialog : Window
{
    private MessageDialog(string title, string message, string[] buttons)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 360;
        MaxWidth = 560;

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        foreach ((string label, int index) in buttons.Select((label, index) => (label, index)))
        {
            Button button = new()
            {
                Content = label,
                MinWidth = 88,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                // The first button is the suggested answer; the last one answers Esc.
                IsDefault = index == 0,
                IsCancel = index == buttons.Length - 1,
            };
            if (index == 0)
            {
                button.Classes.Add("accent");
            }

            button.Click += (_, _) => Close(label);
            buttonRow.Children.Add(button);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttonRow,
            },
        };
    }

    /// <summary>
    /// Shows the question over <paramref name="owner"/>; returns the clicked button's label, or null
    /// when the dialog was closed another way.
    /// </summary>
    public static Task<string?> ShowAsync(Window owner, string title, string message, params string[] buttons)
    {
        return new MessageDialog(title, message, buttons).ShowDialog<string?>(owner);
    }
}
