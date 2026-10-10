using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Arrays
{
    public class ArrayBuilderTests
    {
        private static void Run(Action test)
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                test();
            });
        }

        [Theory]
        [InlineData("SparseMatrixCSC", "Float64", "SparseMatrixCSC{Float64, Int64}")]
        [InlineData("SparseVector", "Int32", "SparseVector{Int32, Int64}")]
        [InlineData("AbstractMatrix", "Float32", "AbstractMatrix{Float32}")]
        [InlineData("SparseMatrixCSC", "Any", "SparseMatrixCSC")]
        public void Sparse_and_abstract_types_are_composed_and_split(string shape, string element, string type)
        {
            Assert.Equal(type, JuliaTypes.Compose(shape, element));
            Assert.Equal((shape, element), JuliaTypes.Split(type));
        }

        [Theory]
        [InlineData("SparseMatrixCSC{Float64, Int64}", "AbstractMatrix{Float64}", true)]
        [InlineData("Matrix{Float64}", "AbstractMatrix{Float64}", true)]
        [InlineData("Matrix{Float64}", "AbstractMatrix", true)]
        [InlineData("SparseVector{Float64, Int64}", "AbstractVector{Float64}", true)]
        [InlineData("SparseMatrixCSC{Float64, Int64}", "Matrix{Float64}", false)]
        [InlineData("Matrix{Float64}", "SparseMatrixCSC{Float64, Int64}", false)]
        [InlineData("SparseVector{Float64, Int64}", "AbstractMatrix{Float64}", false)]
        [InlineData("SparseMatrixCSC{Float32, Int64}", "AbstractMatrix{Float64}", false)]
        [InlineData("AbstractMatrix{Float64}", "Matrix{Float64}", false)]
        [InlineData("SparseMatrixCSC{Float64, Int64}", "SparseMatrixCSC", true)]
        public void Abstract_arrays_take_dense_and_sparse_ones(string output, string parameter, bool compatible)
        {
            Assert.Equal(compatible, Wiring.AreTypesCompatible(output, parameter));
        }

        [Fact]
        public void A_new_builder_makes_a_vector_of_zeros()
        {
            ArrayBuilder builder = new();

            Assert.Null(builder.Error);
            Assert.Equal("zeros(Float64, 100)", builder.Julia);
            Assert.Equal("Vector{Float64}", builder.OutputType);
            Assert.Equal("100-element", builder.Summary);
            Assert.Equal(["n"], builder.Parameters.Select(p => p.Name));
        }

        [Theory]
        [InlineData("zeros", "Matrix", "zeros(Float64, 100, 100)", "Matrix{Float64}")]
        [InlineData("fill", "Vector", "fill(Float64(1), 100)", "Vector{Float64}")]
        [InlineData("randn", "Matrix", "randn(Float64, 100, 100)", "Matrix{Float64}")]
        [InlineData("range", "Matrix", "collect(range(Float64(0), Float64(1); length = 100))", "Vector{Float64}")]
        [InlineData("identity", "Vector", "Matrix{Float64}(I, 100, 100)", "Matrix{Float64}")]
        [InlineData("spzeros", "Vector", "spzeros(Float64, 100)", "SparseVector{Float64, Int64}")]
        [InlineData("sprand", "Matrix", "sprand(Float64, 100, 100, 0.01)", "SparseMatrixCSC{Float64, Int64}")]
        [InlineData("spidentity", "Vector", "spdiagm(ones(Float64, 100))", "SparseMatrixCSC{Float64, Int64}")]
        public void Each_kind_writes_its_Julia_call(string kind, string shape, string julia, string type)
        {
            ArrayBuilder builder = new() { Kind = kind, Shape = shape };

            Assert.Equal(julia, builder.Julia);
            Assert.Equal(type, builder.OutputType);
        }

        [Fact]
        public void Sparse_kinds_need_SparseArrays()
        {
            ArrayBuilder builder = new() { Kind = "sprand", Shape = "Matrix" };

            Assert.Equal("using SparseArrays\nsprand(Float64, 100, 100, 0.01)", builder.Code);
            Assert.Equal("100×100, sparse 1%", builder.Summary);
        }

        [Fact]
        public void Float_only_kinds_switch_away_from_integers()
        {
            ArrayBuilder builder = new() { ElementType = "Int32", Kind = "randn" };

            Assert.Equal("Float64", builder.ElementType);
            Assert.DoesNotContain("Int32", builder.PossibleTypes);
        }

        [Theory]
        [InlineData("n", "-1", "n must be a whole number, 0 or more.")]
        [InlineData("n", "1.5", "n must be a whole number, 0 or more.")]
        [InlineData("density", "2", "density must be a number from 0 to 1.")]
        public void Invalid_arguments_are_explained(string key, string value, string error)
        {
            ArrayBuilder builder = new() { Kind = "sprand" };
            builder.Parameters.OfType<Argument>().Single(a => a.Key == key).Value = value;

            Assert.Equal(error, builder.Error);
        }

        [Fact]
        public void A_diagonal_needs_a_wired_vector()
        {
            ArrayBuilder builder = new() { Kind = "spdiagm" };

            Assert.Equal("Wire a vector into v.", builder.Error);
            Assert.Equal("AbstractVector{Float64}", builder.Parameters.Single().Type);
        }

        [Fact]
        public void A_wired_size_is_used_by_name()
        {
            Run(() =>
            {
                ArrayBuilder builder = (ArrayBuilder)MainViewModel.AddNode(nameof(ArrayBuilder), 30, 60);
                builder.Shape = "Matrix";
                Integer rows = new() { SelectedType = "64-bit" };
                MainViewModel.Nodes.Add(rows);

                Assert.Null(MainViewModel.Connect(rows, builder, builder.Parameters[0]));

                Assert.True(builder.Parameters[0].IsWired);
                Assert.Equal("zeros(Float64, m, 100)", builder.Julia);
                Assert.Equal("m×100", builder.Summary);
            });
        }

        [Fact]
        public void A_sparse_matrix_wires_into_an_abstract_matrix_but_not_a_dense_one()
        {
            Run(() =>
            {
                ArrayBuilder builder = new() { Kind = "sprand", Shape = "Matrix" };
                Parameter any = new("A", "AbstractMatrix{Float64}");
                Parameter dense = new("B", "Matrix{Float64}");
                Function f = new() { Parameters = { any, dense } };
                MainViewModel.Nodes.AddRange([builder, f]);

                Assert.Null(MainViewModel.Connect(builder, f, any));
                Assert.Equal("SparseMatrixCSC{Float64, Int64} can't be passed to B::Matrix{Float64}.", MainViewModel.Connect(builder, f, dense));
            });
        }

        [Fact]
        public void Switching_kinds_keeps_the_wires_of_the_arguments_that_stay()
        {
            Run(() =>
            {
                ArrayBuilder builder = new() { Kind = "sprand" };
                Integer length = new() { SelectedType = "64-bit" };
                Float density = new() { SelectedType = "64-bit" };
                MainViewModel.Nodes.AddRange([builder, length, density]);
                Parameter n = builder.Parameters.Single(p => p.Name == "n");
                Assert.Null(MainViewModel.Connect(length, builder, n));
                Assert.Null(MainViewModel.Connect(density, builder, builder.Parameters.Single(p => p.Name == "density")));

                builder.Kind = "spzeros";

                Connection wire = Assert.Single(MainViewModel.Connections.Where(c => c.TargetID == builder.ID));
                Assert.Equal(n.ID, wire.ParameterID);
                Assert.Equal("spzeros(Float64, n)", builder.Julia);
            });
        }

        [Fact]
        public void A_builder_survives_undo_and_duplication()
        {
            Run(() =>
            {
                ArrayBuilder builder = (ArrayBuilder)MainViewModel.AddNode(nameof(ArrayBuilder), 30, 60);
                builder.Kind = "sprandn";
                builder.Shape = "Matrix";
                builder.ElementType = "Float32";
                builder.Parameters.OfType<Argument>().Single(a => a.Key == "density").Value = "0.05";
                MainViewModel.History.Commit();

                MainViewModel.DuplicateSelection();
                ArrayBuilder copy = MainViewModel.Nodes.OfType<ArrayBuilder>().Single(b => b.IsSelected);
                Assert.NotEqual(builder.ID, copy.ID);
                Assert.Equal("sprandn(Float32, 100, 100, 0.05)", copy.Julia);
                Assert.Empty(copy.AllInputs.Select(p => p.ID).Intersect(builder.AllInputs.Select(p => p.ID)));

                MainViewModel.History.Commit();
                MainViewModel.History.Undo();
                ArrayBuilder restored = Assert.Single(MainViewModel.Nodes.OfType<ArrayBuilder>());
                Assert.Equal("sprandn(Float32, 100, 100, 0.05)", restored.Julia);
            });
        }
    }
}
