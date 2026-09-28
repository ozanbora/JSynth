using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JuliaSynth.Runtime
{
    public enum PlotKind
    {
        /// <summary>
        /// A vector as a line, against its x values or its indices.
        /// </summary>
        Line = 1,

        /// <summary>
        /// A vector as points.
        /// </summary>
        Scatter = 2,

        /// <summary>
        /// A matrix as colours; big ones averaged down in blocks.
        /// </summary>
        Heatmap = 3,

        /// <summary>
        /// Where a matrix has values that aren't zero, as dots: the usual picture of a sparse matrix.
        /// </summary>
        Spy = 4,
    }

    /// <summary>
    /// The numbers of a plot, as a script wrote them (see <see cref="Prelude"/>). Line and scatter plots have
    /// <see cref="X"/> and <see cref="Y"/>; a heatmap has <see cref="Values"/>; a spy plot has the row and
    /// column of each dot in <see cref="Y"/> and <see cref="X"/>.
    /// </summary>
    public sealed class PlotData
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("JSPLOT01");

        /// <summary>
        /// Julia code defining <c>_jsynth_plot(path, kind, y, x)</c>: writes a plot's numbers to <c>path</c>
        /// and returns the path. <c>kind</c> is "auto" (line for a vector, spy for a sparse matrix, heatmap for
        /// another matrix), "line", "scatter", "heatmap" or "spy"; <c>x</c> is nothing or a vector.
        /// Heatmaps are averaged down to at most 1000 cells a side, spy plots to 200,000 dots.
        /// </summary>
        public static string Prelude { get; } = """
            function _jsynth_plot(path, kind, y, x = nothing)
                issparse = typeof(y).name.name in (:SparseMatrixCSC, :SparseVector)
                if kind == "auto"
                    kind = y isa AbstractVector ? "line" : issparse ? "spy" : "heatmap"
                end
                if kind in ("line", "scatter")
                    y isa AbstractVector || throw(ArgumentError("a $kind plot needs a vector, not a $(typeof(y))"))
                    ys = Float64.(collect(y))
                    xs = x === nothing ? collect(1.0:length(ys)) : Float64.(collect(x))
                    length(xs) == length(ys) || throw(DimensionMismatch("$(length(xs)) x values for $(length(ys)) y values"))
                    header, data = (kind == "line" ? 1 : 2, length(ys), 0), (xs, ys)
                elseif kind == "heatmap"
                    y isa AbstractMatrix || throw(ArgumentError("a heatmap needs a matrix, not a $(typeof(y))"))
                    values = _jsynth_shrink(y, 1000)
                    header, data = (3, size(values, 1), size(values, 2)), (vec(values),)
                elseif kind == "spy"
                    y isa AbstractMatrix || throw(ArgumentError("a spy plot needs a matrix, not a $(typeof(y))"))
                    rows, cols = if issparse
                        r, c, _ = parentmodule(typeof(y)).findnz(y)
                        r, c
                    else
                        found = findall(!iszero, y)
                        first.(Tuple.(found)), last.(Tuple.(found))
                    end
                    keep = length(rows) <= 200_000 ? eachindex(rows) : round.(Int, range(1, length(rows); length = 200_000))
                    header, data = (4, length(keep), 0), (Float64.(cols[keep]), Float64.(rows[keep]))
                else
                    throw(ArgumentError("unknown kind of plot: $kind"))
                end
                open(path, "w") do io
                    write(io, codeunits("JSPLOT01"))
                    foreach(n -> write(io, Int64(n)), (header..., size(y, 1), ndims(y) == 1 ? 1 : size(y, 2)))
                    foreach(d -> write(io, d), data)
                end
                path
            end
            function _jsynth_shrink(A, limit)
                r, c = size(A)
                f = max(1, cld(max(r, c), limit))
                f == 1 && return Float64.(Matrix(A))
                B = zeros(cld(r, f), cld(c, f))
                for j in axes(B, 2), i in axes(B, 1)
                    block = view(A, (i - 1) * f + 1:min(i * f, r), (j - 1) * f + 1:min(j * f, c))
                    B[i, j] = sum(block) / length(block)
                end
                B
            end
            """.ReplaceLineEndings("\n");

        public PlotKind Kind { get; }

        /// <summary>
        /// Line and scatter: the x values. Spy: the column of each dot.
        /// </summary>
        public double[] X { get; }

        /// <summary>
        /// Line and scatter: the y values. Spy: the row of each dot.
        /// </summary>
        public double[] Y { get; }

        /// <summary>
        /// Heatmap: the (possibly averaged down) values, [row, column].
        /// </summary>
        public double[,]? Values { get; }

        /// <summary>
        /// Size of the plotted value before any averaging down: a vector has one column.
        /// </summary>
        public long Rows { get; }

        public long Columns { get; }

        private PlotData(PlotKind kind, double[] x, double[] y, double[,]? values, long rows, long columns)
        {
            (Kind, X, Y, Values, Rows, Columns) = (kind, x, y, values, rows, columns);
        }

        /// <summary>
        /// Reads a file written by <c>_jsynth_plot</c>.
        /// </summary>
        /// <exception cref="InvalidDataException">It isn't one.</exception>
        /// <exception cref="IOException">It can't be read.</exception>
        public static PlotData Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < Magic.Length + (5 * sizeof(long)) || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                throw new InvalidDataException("This is not a plot file.");
            }

            ReadOnlySpan<long> header = MemoryMarshal.Cast<byte, long>(bytes.AsSpan(Magic.Length, 5 * sizeof(long)));
            ReadOnlySpan<byte> data = bytes.AsSpan(Magic.Length + (5 * sizeof(long)));
            (long kind, long a, long b, long rows, long columns) = (header[0], header[1], header[2], header[3], header[4]);

            // Julia writes in the machine's byte order: little-endian on every platform it and .NET run on here.
            if (!BitConverter.IsLittleEndian || !Enum.IsDefined(typeof(PlotKind), (int)kind) || a < 0 || b < 0)
            {
                throw new InvalidDataException("This plot file can't be read.");
            }

            ReadOnlySpan<double> numbers = MemoryMarshal.Cast<byte, double>(data);
            switch ((PlotKind)kind)
            {
                case PlotKind.Heatmap:
                {
                    Expect(numbers, a * b);
                    double[,] values = new double[a, b];

                    // Julia stores matrices column by column.
                    for (int j = 0; j < b; j++)
                    {
                        for (int i = 0; i < a; i++)
                        {
                            values[i, j] = numbers[(int)((j * a) + i)];
                        }
                    }

                    return new PlotData(PlotKind.Heatmap, [], [], values, rows, columns);
                }

                default:
                    Expect(numbers, 2 * a);
                    return new PlotData((PlotKind)kind, numbers[..(int)a].ToArray(), numbers[(int)a..(int)(2 * a)].ToArray(), null, rows, columns);
            }
        }

        private static void Expect(ReadOnlySpan<double> numbers, long count)
        {
            if (numbers.Length != count)
            {
                throw new InvalidDataException($"The plot file holds {numbers.Length} numbers, not {count}.");
            }
        }
    }
}
