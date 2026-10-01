using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using JuliaSynth.Controls;
using JuliaSynth.Model;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Standard library functions in function bodies: suggested with their package, and pointed out
    /// when used without it.
    /// </summary>
    public class StdlibTests
    {
        private static Function Stats(string body, string packages = "")
        {
            Function function = new() { Name = "stats", Packages = packages, Body = body };
            function.Parameters.Add(new Parameter("v", Function.AnyType));
            return function;
        }

        private static string Hints(Function function)
        {
            return string.Join("; ", function.PackageHints.Select(h => $"{h.Uses} {h.Verb} {h.Package}"));
        }

        [Fact]
        public void Names_from_a_library_the_function_does_not_load_are_pointed_out()
        {
            Assert.Equal("mean, std are in Statistics; norm is in LinearAlgebra", Hints(Stats("mean(v) / std(v) + norm(v) - mean(v)")));
        }

        [Fact]
        public void Loading_the_package_one_way_or_another_answers_the_hint()
        {
            Assert.Empty(Stats("mean(v)", packages: "Statistics").PackageHints);
            Assert.Empty(Stats("using Statistics\nmean(v)").PackageHints);
            Assert.Empty(Stats("import Statistics: mean\nmean(v)").PackageHints);

            Function sparse = Stats("nnz(v)");
            Assert.Equal("nnz is in SparseArrays", Hints(sparse));
            sparse.Parameters[0].Shape = "SparseVector";
            Assert.Empty(sparse.PackageHints);
        }

        [Fact]
        public void The_bodys_own_names_comments_strings_and_fields_are_not_pointed_out()
        {
            Assert.Empty(Stats("mean = sum(v) / length(v)\nmean").PackageHints);
            Assert.Empty(Stats("# the mean of v\nprintln(\"mean\")\nv.norm").PackageHints);
            // A comparison isn't an assignment.
            Assert.Equal("mean is in Statistics", Hints(Stats("mean == 1")));

            Function shadowed = Stats("norm(v)");
            shadowed.Parameters[0].Name = "norm";
            Assert.Empty(shadowed.PackageHints);
        }

        [Fact]
        public void A_qualified_name_points_out_its_module()
        {
            Assert.Equal("LinearAlgebra.norm is in LinearAlgebra", Hints(Stats("LinearAlgebra.norm(v)")));
            Assert.Empty(Stats("LinearAlgebra.norm(v)", packages: "LinearAlgebra").PackageHints);
        }

        [Fact]
        public void Adding_a_package_appends_it_once()
        {
            Function function = Stats("mean(v)");

            function.AddPackage("Statistics");
            function.AddPackage("LinearAlgebra");
            function.AddPackage("Statistics");

            Assert.Equal("Statistics, LinearAlgebra", function.Packages);
            Assert.Empty(function.PackageHints);
        }

        [Fact]
        public void Library_functions_are_suggested_with_the_package_they_would_add()
        {
            Function function = Stats(string.Empty, packages: "LinearAlgebra");
            var all = JuliaCompletions.All(function, []);

            JuliaCompletion mean = all.Single(c => c.Text == "mean");
            Assert.Equal("Statistics", mean.Package);
            Assert.EndsWith("Picking it adds Statistics to Packages.", mean.Detail);

            JuliaCompletion norm = all.Single(c => c.Text == "norm");
            Assert.Null(norm.Package);
            Assert.StartsWith("LinearAlgebra: norm(A, p=2)", norm.Detail);

            // Base's functions come first.
            Assert.True(all.ToList().FindIndex(c => c.Text == "sum") < all.ToList().FindIndex(c => c.Text == "mean"));
        }

        [Fact]
        public void Picking_a_library_function_adds_its_package()
        {
            Ui.Run(() =>
            {
                Function function = Stats(string.Empty);
                Window window = new() { Content = new Views.Details.Function { DataContext = function }, Width = 640, Height = 600 };
                window.Show();
                JuliaCodeEditor editor = window.GetLogicalDescendants().OfType<JuliaCodeEditor>().Single();
                editor.TextArea.Focus();

                window.KeyTextInput("m");
                window.KeyTextInput("e");
                window.KeyTextInput("a");
                Assert.Equal("mean", editor.Completion?.CompletionList.SelectedItem?.Text);
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

                Assert.Equal("mean", function.Body);
                Assert.Equal("Statistics", function.Packages);
                window.Close();
            });
        }

        [Fact]
        public void The_add_button_of_a_hint_adds_the_package()
        {
            Ui.Run(() =>
            {
                Function function = Stats("std(v)");
                Window window = new() { Content = new Views.Details.Function { DataContext = function }, Width = 640, Height = 600 };
                window.Show();

                Button add = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Add Statistics");
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("Statistics", function.Packages);
                Assert.Empty(function.PackageHints);
                window.Close();
            });
        }
    }
}
