using JuliaSynth.Model;
using Xunit;

namespace JuliaSynth.Tests
{
    public class WiringTests
    {
        [Theory]
        [InlineData("Float64", "Float64", true)]
        [InlineData("Float32", "Float64", false)]
        [InlineData("UInt32", "Int32", false)]
        [InlineData("Any", "Int64", true)]
        [InlineData("Float32", "Any", true)]
        public void Types_must_match_unless_one_side_is_Any(string output, string parameter, bool compatible)
        {
            Assert.Equal(compatible, Wiring.AreTypesCompatible(output, parameter));
        }

        [Fact]
        public void Numeric_nodes_output_the_Julia_type_of_their_bitness()
        {
            Assert.Equal("Float64", new Float { SelectedType = "64-bit" }.OutputType);
            Assert.Equal("Int8", new Integer { SelectedType = "8-bit" }.OutputType);
            Assert.Equal("UInt16", new UnsignedInteger { SelectedType = "16-bit" }.OutputType);
            Assert.Equal("Float32", new Function { ReturnType = "Float32" }.OutputType);
        }

        [Fact]
        public void A_mismatched_type_is_rejected_with_the_reason()
        {
            Integer z = new() { SelectedType = "32-bit" };
            Parameter y = new("y", "Float64");
            Function f = new() { Parameters = { y } };

            Assert.Equal("Int32 can't be passed to y::Float64.", Wiring.Validate([], z, f, y));
        }

        [Fact]
        public void A_compatible_connection_is_allowed()
        {
            Float x = new() { SelectedType = "64-bit" };
            Parameter p = new("x", "Float64");
            Function f = new() { Parameters = { p } };

            Assert.Null(Wiring.Validate([], x, f, p));
        }

        [Fact]
        public void A_function_cannot_feed_itself()
        {
            Parameter p = new("x", Function.AnyType);
            Function f = new() { Parameters = { p } };

            Assert.Equal("This connection would create a loop.", Wiring.Validate([], f, f, p));
        }

        [Fact]
        public void An_indirect_loop_is_rejected()
        {
            Parameter fx = new("x", Function.AnyType);
            Parameter gy = new("y", Function.AnyType);
            Function f = new() { Parameters = { fx } };
            Function g = new() { Parameters = { gy } };
            Connection fToG = new(f.ID, g.ID, gy.ID);

            // f already feeds g, so g feeding f would close the loop.
            Assert.NotNull(Wiring.Validate([fToG], g, f, fx));
        }

        [Fact]
        public void A_wire_is_detached_once_its_parameter_is_gone()
        {
            Float x = new();
            Parameter p = new("x", Function.AnyType);
            Function f = new() { Parameters = { p } };
            Connection wire = new(x.ID, f.ID, p.ID);

            Assert.True(Wiring.IsAttached([x, f], wire));
            f.RemoveParameter(p);
            Assert.False(Wiring.IsAttached([x, f], wire));
        }
    }
}
