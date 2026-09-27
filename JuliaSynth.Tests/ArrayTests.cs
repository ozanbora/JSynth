using JuliaSynth.Model;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    public class ArrayTests
    {
        [Theory]
        [InlineData("Int8", "127", true)]
        [InlineData("Int8", "128", false)]
        [InlineData("UInt8", "-1", false)]
        [InlineData("UInt64", "18446744073709551615", true)]
        [InlineData("Int64", "1_000_000", true)]
        [InlineData("Int64", "1.5", false)]
        [InlineData("Float64", "1e-3", true)]
        [InlineData("Float64", "-Inf", true)]
        [InlineData("Float32", "NaN", true)]
        [InlineData("Float64", "abc", false)]
        [InlineData("Bool", "true", true)]
        [InlineData("Bool", "1", false)]
        public void Values_are_checked_against_the_element_type(string type, string literal, bool valid)
        {
            Assert.Equal(valid, JuliaTypes.IsValidLiteral(type, literal));
        }

        [Theory]
        [InlineData("[1 2; 3 4]", 2, 2)]
        [InlineData("[1, 2, 3]", 1, 3)]
        [InlineData("1 2 3\n4 5 6", 2, 3)]
        [InlineData("[1; 2; 3]", 3, 1)]
        [InlineData("[]", 0, 0)]
        public void Julia_style_values_are_read_row_by_row(string text, int rows, int columns)
        {
            (var parsed, string? error) = ArrayLiteral.Parse(text, "Int64");

            Assert.Null(error);
            Assert.Equal(rows, parsed!.Count);
            Assert.All(parsed, row => Assert.Equal(columns, row.Count));
        }

        [Theory]
        [InlineData("[1 2; 3 4 5]", "Row 2 has 3 values, row 1 has 2.")]
        [InlineData("[1 2.5]", "'2.5' is not a valid Int64.")]
        [InlineData("[1 2", "Brackets don't match.")]
        public void Mistakes_are_explained(string text, string error)
        {
            Assert.Equal(error, ArrayLiteral.Parse(text, "Int64").Error);
        }

        [Fact]
        public void A_vector_node_describes_itself()
        {
            VectorNode vector = new();

            Assert.Null(vector.Error);
            Assert.Equal("3-element", vector.Size);
            Assert.Equal("Vector{Float64}", vector.OutputType);
            Assert.Equal("Float64[1.0, 2.0, 3.0]", vector.Julia);
        }

        [Fact]
        public void A_vector_can_be_a_row_or_a_column_but_not_a_matrix()
        {
            VectorNode vector = new() { ElementType = "Int64", Text = "[1; 2; 3; 4]" };
            Assert.Equal("4-element", vector.Size);

            vector.Text = "[1 2; 3 4]";
            Assert.NotNull(vector.Error);
            Assert.Contains("use a matrix", vector.Error);
        }

        [Fact]
        public void A_matrix_node_describes_itself()
        {
            MatrixNode matrix = new();

            Assert.Null(matrix.Error);
            Assert.Equal("2×2", matrix.Size);
            Assert.Equal("Matrix{Float64}", matrix.OutputType);
            Assert.Equal("Float64[1.0 0.0; 0.0 1.0]", matrix.Julia);
        }

        [Fact]
        public void Changing_the_element_type_rechecks_the_values()
        {
            MatrixNode matrix = new();

            matrix.ElementType = "Int64";

            Assert.Equal("'1.0' is not a valid Int64.", matrix.Error);
            Assert.Equal("?", matrix.Size);
            Assert.Equal("Matrix{Int64}", matrix.OutputType);
        }

        [Fact]
        public void The_preview_leaves_out_what_does_not_fit()
        {
            MatrixNode matrix = new() { Text = "1 2 3 4 5; 6 7 8 9 10; 1 1 1 1 1; 2 2 2 2 2" };

            string[] lines = matrix.Preview.Split('\n');

            Assert.Equal(4, lines.Length);
            Assert.EndsWith("⋯", lines[0]);
            Assert.Equal("⋮", lines[3]);
        }

        [Fact]
        public void Vectors_and_matrices_wire_to_parameters_of_their_exact_type()
        {
            MatrixNode matrix = new();
            Parameter m = new("m", "Matrix{Float64}");
            Parameter v = new("v", "Vector{Float64}");
            Function f = new() { Parameters = { m, v } };

            Assert.Null(Wiring.Validate([], matrix, f, m));
            Assert.Equal("Matrix{Float64} can't be passed to v::Vector{Float64}.", Wiring.Validate([], matrix, f, v));
        }

        [Fact]
        public void Functions_offer_vector_and_matrix_types()
        {
            Assert.Contains("Vector{Float64}", Function.Types);
            Assert.Contains("Matrix{Int32}", Function.Types);
            Assert.Equal(1 + (3 * JuliaTypes.Numeric.Count), Function.Types.Count);
            Assert.Equal(Function.Types.Count, Function.Types.Distinct().Count());
        }
    }
}
