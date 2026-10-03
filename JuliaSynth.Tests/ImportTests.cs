using JuliaSynth.Model;
using JuliaSynth.ViewModels;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Packages in function nodes: the Packages field, and using/import lines typed in a body, which
    /// Julia only allows outside the function.
    /// </summary>
    public class ImportTests
    {
        [Fact]
        public void Using_and_import_lines_are_taken_out_of_a_body_wherever_they_are()
        {
            (var statements, string rest) = JuliaImports.Split("using LinearAlgebra\nn = norm(v)\n  import Statistics: mean, # averages\n    std\nusing_count = 1\nmean(v) / n");

            Assert.Equal(["using LinearAlgebra", "import Statistics: mean, std"], statements);
            Assert.Equal("n = norm(v)\nusing_count = 1\nmean(v) / n", rest);
        }

        [Theory]
        [InlineData("using LinearAlgebra", "LinearAlgebra")]
        [InlineData("using LinearAlgebra, Statistics", "LinearAlgebra,Statistics")]
        [InlineData("using LinearAlgebra: norm, dot", "LinearAlgebra")]
        [InlineData("import Statistics: mean as average", "Statistics")]
        [InlineData("import DSP", "DSP")]
        [InlineData("import DSP as D", "DSP")]
        [InlineData("using Base.Iterators, SparseArrays  # comment", "SparseArrays")]
        [InlineData("import Base: show", "")]
        public void A_statement_names_the_packages_it_loads(string statement, string expected)
        {
            (var packages, string? problem) = JuliaImports.PackagesIn(statement);

            Assert.Null(problem);
            Assert.Equal(expected, string.Join(",", packages));
        }

        [Theory]
        [InlineData("using .Helpers")]
        [InlineData("using 3D")]
        public void A_statement_a_function_node_cannot_have_is_a_problem(string statement)
        {
            Assert.NotNull(JuliaImports.PackagesIn(statement).Problem);
        }

        [Fact]
        public void The_packages_field_takes_names_separated_by_commas_or_spaces()
        {
            Assert.Equal(["LinearAlgebra", "Statistics", "DSP"], JuliaImports.ParseList(" LinearAlgebra,Statistics  DSP, Base,").Packages);
            Assert.Null(JuliaImports.ParseList("LinearAlgebra").Invalid);
            Assert.Equal("Linear-Algebra", JuliaImports.ParseList("Linear-Algebra, DSP").Invalid);
            Assert.Empty(JuliaImports.ParseList(null).Packages);
        }

        [Fact]
        public void A_function_loads_its_packages_before_its_definition()
        {
            Function function = new() { Name = "rms", Packages = "LinearAlgebra", Body = "import Statistics: mean\nnorm(v) / sqrt(length(v))" };
            function.Parameters.Add(new Parameter("v", Function.AnyType));

            Assert.Null(function.Error);
            Assert.Equal(["LinearAlgebra", "Statistics"], function.RequiredPackages);
            Assert.Equal("using LinearAlgebra\nimport Statistics: mean\nfunction rms(v)\n    norm(v) / sqrt(length(v))\nend", function.Code);
        }

        [Fact]
        public void A_bad_package_name_or_statement_is_an_error_on_the_function()
        {
            Function function = new() { Packages = "Linear Algebra!" };
            Assert.Equal("'Algebra!' is not a package name.", function.Error);

            function.Packages = string.Empty;
            function.Body = "using .Helpers\n1";
            Assert.Contains("module of this script", function.Error);
        }

        [Fact]
        public void A_run_checks_a_functions_packages_and_skips_it_when_one_is_missing()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
                f.Packages = "LinearAlgebra";
                f.Body = "import Statistics\nx ^ z";

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);
                JuliaScript plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false);

                // Checked and offered for installing before a run, in both.
                Assert.Equal(["LinearAlgebra", "Statistics"], run.Packages);
                Assert.Equal(["LinearAlgebra", "Statistics"], plain.Packages);

                // Loaded up front with import (to find missing ones); the function's own code takes the names.
                Assert.Contains("for (_p, _how) in ((\"LinearAlgebra\", \"import\"), (\"Statistics\", \"import\"),)", run.Text);
                Assert.Contains("if (_why = _jsynth_package_problem(\"LinearAlgebra\", \"Statistics\")) !== nothing\n    _jsynth_skip(", run.Text);
                Assert.Contains("include_string(@__MODULE__, \"using LinearAlgebra\\nimport Statistics\\nfunction f(", run.Text);

                // A plain script has them with the function, not in a using line of its own.
                Assert.Contains("using LinearAlgebra\nimport Statistics\nfunction f(", plain.Text);
                Assert.DoesNotContain("\nusing LinearAlgebra\n\n", plain.Text);
            });
        }

        [Fact]
        public void Packages_survive_saving_and_copying()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                Function f = MainViewModel.Nodes.OfType<Function>().Single(n => n.Name == "f");
                f.Packages = "LinearAlgebra";
                f.IsSelected = true;

                MainViewModel.DuplicateSelection();

                Function copy = MainViewModel.Nodes.OfType<Function>().Last();
                Assert.NotSame(f, copy);
                Assert.Equal("LinearAlgebra", copy.Packages);
            });
        }
    }
}
