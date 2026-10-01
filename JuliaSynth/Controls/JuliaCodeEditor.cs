using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using TextMateSharp.Grammars;

namespace JuliaSynth.Controls;

/// <summary>
/// A Julia code editor: syntax colours, and suggestions while typing a name (or on Ctrl+Space).
/// </summary>
public class JuliaCodeEditor : TextEditor
{
    /// <summary>
    /// The code, bindable both ways (unlike <see cref="TextEditor.Text"/>).
    /// </summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<JuliaCodeEditor, string?>(nameof(Code), defaultBindingMode: BindingMode.TwoWay);

    private readonly RegistryOptions _grammars = new(ThemeName.LightPlus);
    private TextMate.Installation? _colours;
    private CompletionWindow? _completion;

    public JuliaCodeEditor()
    {
        TextArea.TextEntered += TextEntered;
        ActualThemeVariantChanged += (_, _) => _colours?.SetTheme(_grammars.LoadTheme(ColourTheme));
        TextArea.AddHandler(KeyDownEvent, CompletionKeyDown, RoutingStrategies.Tunnel);
    }

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>
    /// Everything that could be suggested, asked for each time suggestions open; none when null.
    /// </summary>
    public Func<IReadOnlyList<JuliaCompletion>>? Completions { get; set; }

    /// <summary>
    /// The open suggestion list, if any; tests look at it.
    /// </summary>
    internal CompletionWindow? Completion => _completion;

    // Styled like the TextEditor it is.
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <summary>
    /// Opens the suggestions for the name before the caret; false when there are none.
    /// </summary>
    /// <param name="typing">Opened by typing: not for an empty name, nor when the name is already complete.</param>
    public bool ShowCompletions(bool typing)
    {
        _completion?.Close();

        string? word = JuliaCompletions.WordBefore(Text, CaretOffset);
        if (word is null || Completions is null || (typing && word.Length == 0))
        {
            return false;
        }

        IReadOnlyList<JuliaCompletion> matching = JuliaCompletions.Matching(Completions(), word);
        if (matching.Count == 0 || (typing && matching.Count == 1 && matching[0].Text == word))
        {
            return false;
        }

        CompletionWindow window = new(TextArea)
        {
            StartOffset = CaretOffset - word.Length,
            EndOffset = CaretOffset,
            CloseWhenCaretAtBeginning = typing,
        };

        IList<ICompletionData> items = window.CompletionList.CompletionData;
        foreach (JuliaCompletion completion in matching)
        {
            items.Add(new Suggestion(completion));
        }

        window.Closed += (_, _) =>
        {
            if (_completion == window)
            {
                _completion = null;
            }
        };

        _completion = window;
        window.Show();
        window.CompletionList.SelectItem(word);
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CodeProperty)
        {
            string code = change.GetNewValue<string?>() ?? string.Empty;
            if (Text != code)
            {
                Text = code;
            }
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        if (Code != Text)
        {
            SetCurrentValue(CodeProperty, Text);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _colours = this.InstallTextMate(_grammars);
        _colours.SetTheme(_grammars.LoadTheme(ColourTheme));
        _colours.SetGrammar(_grammars.GetScopeByLanguageId(_grammars.GetLanguageByExtension(".jl").Id));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _completion?.Close();
        _colours?.Dispose();
        _colours = null;
    }

    private ThemeName ColourTheme => ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;

    private void TextEntered(object? sender, TextInputEventArgs e)
    {
        if (_completion is null && e.Text is [char typed] && JuliaCompletions.IsNamePart(typed))
        {
            _ = ShowCompletions(typing: true);
        }
        else if (_completion is { CompletionList.CurrentList.Count: 0 })
        {
            // Nothing left that starts with what was typed.
            _completion.Close();
        }
    }

    private void CompletionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = ShowCompletions(typing: false);
            e.Handled = true;
        }
    }

    /// <summary>
    /// One suggestion as the completion list shows it: the name, with its line as the tooltip.
    /// </summary>
    private sealed class Suggestion(JuliaCompletion completion) : ICompletionData
    {
        public IImage? Image => null;

        public string Text => completion.Text;

        public object Content => completion.Text;

        public object Description => completion.Kind switch
        {
            CompletionKind.Keyword => $"Keyword. {completion.Detail}",
            CompletionKind.Constant => $"Constant. {completion.Detail}",
            CompletionKind.Function => $"Function in this design: {completion.Detail}",
            _ => completion.Detail,
        };

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, completion.Text);
        }
    }
}
