using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using JSynth.Controls;
using JSynth.Model;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Code
{
    /// <summary>
    /// Suggestions while typing a function body, and the editor that shows them.
    /// </summary>
    public class CompletionTests
    {
        private static Function Square()
        {
            Function function = new() { Name = "square" };
            function.Parameters.Add(new Parameter("x", "Float64"));
            function.Parameters.Add(new Parameter("sum", Function.AnyType));
            return function;
        }

        [Fact]
        public void A_body_is_offered_its_parameters_first_then_other_functions_keywords_and_Base()
        {
            Function other = new() { Name = "scale" };
            IReadOnlyList<JuliaCompletion> all = JuliaCompletions.All(Square(), [other]);

            Assert.Equal(["x", "sum", "scale", "square"], all.Take(4).Select(c => c.Text));
            Assert.Equal("Parameter x::Float64", all[0].Detail);
            Assert.Equal("scale()", all[2].Detail);
            Assert.Contains(all, c => c.Text == "return" && c.Kind == CompletionKind.Keyword);
            Assert.Contains(all, c => c.Text == "π" && c.Kind == CompletionKind.Constant);
            Assert.Contains(all, c => c.Text == "push!" && c.Kind == CompletionKind.Builtin);

            // The parameter named sum hides Base's sum.
            Assert.Single(all, c => c.Text == "sum");
        }

        [Fact]
        public void A_function_with_an_invalid_name_is_not_offered()
        {
            Function broken = new() { Name = "not valid" };

            Assert.DoesNotContain(JuliaCompletions.All(Square(), [broken]), c => c.Text == "not valid");
        }

        [Fact]
        public void Matches_ignore_case_but_put_the_exact_case_first()
        {
            IReadOnlyList<JuliaCompletion> all =
            [
                new("Inf", "", CompletionKind.Constant),
                new("in", "", CompletionKind.Keyword),
                new("insert!", "", CompletionKind.Builtin),
                new("sqrt", "", CompletionKind.Builtin),
            ];

            Assert.Equal(["in", "insert!", "Inf"], JuliaCompletions.Matching(all, "in").Select(c => c.Text));
            Assert.Empty(JuliaCompletions.Matching(all, "zz"));
        }

        [Theory]
        [InlineData("y = sq", "sq")]
        [InlineData("y = push!", "push!")]
        [InlineData("a = 1\nb = co", "co")]
        [InlineData("x + ", "")]
        [InlineData("", "")]
        [InlineData("c = '#'; sq", "sq")]
        [InlineData("c = '\\''; sq", "sq")]
        [InlineData("s = \"a\\\"b\" * st", "st")]
        [InlineData("y = 2pi", null)]
        [InlineData("y = 1 # sq", null)]
        [InlineData("s = \"sq", null)]
        public void The_word_before_the_caret_is_completed_only_in_code(string text, string? expected)
        {
            Assert.Equal(expected, JuliaCompletions.WordBefore(text, text.Length));
        }

        [Fact]
        public void A_comment_on_an_earlier_line_does_not_stop_suggestions()
        {
            const string text = "# square it\nsq";

            Assert.Equal("sq", JuliaCompletions.WordBefore(text, text.Length));
        }

        [Fact]
        public void Typing_in_the_body_suggests_names_and_Enter_puts_one_in()
        {
            Ui.Run(() =>
            {
                Function function = Square();
                function.Body = "y = 1";
                Window window = new() { Content = new Views.Details.Function { DataContext = function }, Width = 640, Height = 480 };
                window.Show();

                JuliaCodeEditor editor = window.GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();
                Assert.Equal("y = 1", editor.Text);

                editor.TextArea.Focus();
                editor.CaretOffset = editor.Text.Length;
                window.KeyTextInput("\ns");
                window.KeyTextInput("q");

                // The function itself (square) comes before Base's sqrt; typing more narrows it down.
                Assert.NotNull(editor.Completion);
                Assert.Equal("square", editor.Completion!.CompletionList.SelectedItem?.Text);

                window.KeyTextInput("r");
                Assert.Equal("sqrt", editor.Completion!.CompletionList.SelectedItem?.Text);

                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

                Assert.Null(editor.Completion);
                Assert.Equal("y = 1\nsqrt", function.Body);
                window.Close();
            });
        }

        [Fact]
        public void Ctrl_Space_lists_everything_and_typing_narrows_it_down()
        {
            Ui.Run(() =>
            {
                Function function = Square();
                Window window = new() { Content = new Views.Details.Function { DataContext = function }, Width = 640, Height = 480 };
                window.Show();

                JuliaCodeEditor editor = window.GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();
                editor.TextArea.Focus();
                window.KeyPress(Key.Space, RawInputModifiers.Control, PhysicalKey.Space, null);

                Assert.NotNull(editor.Completion);
                Assert.Equal("x", editor.Completion!.CompletionList.SelectedItem?.Text);

                window.KeyTextInput("zzz");
                Assert.Null(editor.Completion);
                Assert.Equal("zzz", function.Body);
                window.Close();
            });
        }

        [Fact]
        public void Suggestions_inside_the_node_flyout_leave_the_flyout_open()
        {
            Ui.Run(() =>
            {
                Function function = Square();
                Flyout flyout = new() { Content = new Views.Details.Function { DataContext = function }, ShowMode = FlyoutShowMode.Transient };
                Button button = new() { Content = "Ξ", Flyout = flyout };
                Window window = new() { Content = button, Width = 800, Height = 600 };
                window.Show();
                flyout.ShowAt(button);
                Dispatcher.UIThread.RunJobs();

                JuliaCodeEditor editor = ((Control)flyout.Content!).GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();
                editor.TextArea.Focus();
                TopLevel flyoutTop = TopLevel.GetTopLevel(editor)!;
                flyoutTop.KeyTextInput("l");
                flyoutTop.KeyTextInput("e");
                flyoutTop.KeyTextInput("n");

                Assert.NotNull(editor.Completion);
                Assert.True(flyout.IsOpen);

                flyoutTop.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

                Assert.True(flyout.IsOpen);
                Assert.Equal("length", function.Body);
                flyout.Hide();
                window.Close();
            });
        }

        [Fact]
        public void A_body_changed_elsewhere_shows_in_the_editor()
        {
            Ui.Run(() =>
            {
                Function function = Square();
                Window window = new() { Content = new Views.Details.Function { DataContext = function }, Width = 640, Height = 480 };
                window.Show();
                JuliaCodeEditor editor = window.GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();

                function.Body = "x^2";

                Assert.Equal("x^2", editor.Text);
                window.Close();
            });
        }
    }
}
