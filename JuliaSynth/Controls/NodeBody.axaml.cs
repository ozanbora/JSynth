using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.ViewModels;

namespace JuliaSynth.Controls;

/// <summary>
/// The body of a node with an input row per parameter and one output: its glyph, title, the type it gives, what it
/// does (the 𝑖 tooltip), the delete button, and its ports. A node's view sets the parts that are its own.
/// </summary>
public partial class NodeBody : UserControl
{
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<NodeBody, string?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<NodeBody, string?>(nameof(Title));

    public static readonly StyledProperty<string?> InfoProperty = AvaloniaProperty.Register<NodeBody, string?>(nameof(Info));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<NodeBody, object?>(nameof(Actions));

    public static readonly StyledProperty<object?> ExtraProperty = AvaloniaProperty.Register<NodeBody, object?>(nameof(Extra));

    public NodeBody()
    {
        // The ports stick out of the node, and its shadow falls outside it: as for the node itself (see Node).
        ClipToBounds = false;
        InitializeComponent();
    }

    /// <summary>
    /// The symbol at the top left: "if", "⋈", ...
    /// </summary>
    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>
    /// The name next to the output: "only when", "zip", ...
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// What the node does, shown over the 𝑖.
    /// </summary>
    public string? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    /// <summary>
    /// Buttons of the node's own, before the 𝑖: switching how it works, say.
    /// </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>
    /// More over the inputs' rows, on the right: another output, say.
    /// </summary>
    public object? Extra
    {
        get => GetValue(ExtraProperty);
        set => SetValue(ExtraProperty, value);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.Node node)
        {
            _ = MainViewModel.Nodes.Remove(node);
        }
    }
}
