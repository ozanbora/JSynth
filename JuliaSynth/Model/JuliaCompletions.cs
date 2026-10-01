using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// What a suggestion is, which decides its order: the function's own names first.
    /// </summary>
    public enum CompletionKind
    {
        Parameter,
        Function,
        Keyword,
        Constant,
        Builtin,
    }

    /// <summary>
    /// A word to suggest while typing a function body, with a line about it.
    /// </summary>
    /// <param name="Package">The package choosing it adds to the function; null when nothing needs adding.</param>
    public sealed record JuliaCompletion(string Text, string Detail, CompletionKind Kind, string? Package = null);

    /// <summary>
    /// Suggestions for a function body: its parameters, the design's other functions, Julia's keywords
    /// and constants, common functions of Base, then of the standard libraries (see <see cref="JuliaStdlib"/>).
    /// </summary>
    public static class JuliaCompletions
    {
        private static readonly JuliaCompletion[] Keywords = new (string, string)[]
        {
            ("if", "if condition … elseif … else … end"),
            ("elseif", "Another condition of an if block."),
            ("else", "What to do when no condition holds."),
            ("end", "Ends a block."),
            ("for", "for x in collection … end"),
            ("while", "while condition … end"),
            ("in", "for x in collection; also x in collection, a membership test."),
            ("isa", "x isa T: whether x is of type T."),
            ("begin", "begin … end: a block of expressions."),
            ("let", "let x = … end: a block with its own variables."),
            ("do", "f(args) do x … end: passes the block as a function."),
            ("return", "Returns a value from the function."),
            ("break", "Leaves the loop."),
            ("continue", "Goes on with the next iteration."),
            ("local", "Declares a variable local to the block."),
            ("global", "Refers to a global variable."),
            ("try", "try … catch e … finally … end"),
            ("catch", "Handles an error thrown in a try block."),
            ("finally", "Runs whether or not an error was thrown."),
            ("function", "An inner function: function name(args) … end"),
        }.Select(k => new JuliaCompletion(k.Item1, k.Item2, CompletionKind.Keyword)).ToArray();

        private static readonly JuliaCompletion[] Constants = new (string, string)[]
        {
            ("true", "Bool"),
            ("false", "Bool"),
            ("nothing", "No value."),
            ("missing", "A missing value."),
            ("pi", "π = 3.14159…"),
            ("π", "pi = 3.14159…"),
            ("ℯ", "Euler's number, 2.71828…"),
            ("Inf", "Positive infinity (Float64)."),
            ("NaN", "Not a number (Float64)."),
            ("eps", "eps(T): the gap between 1 and the next number of type T."),
        }.Select(c => new JuliaCompletion(c.Item1, c.Item2, CompletionKind.Constant)).ToArray();

        private static readonly JuliaCompletion[] Builtins = new (string, string)[]
        {
            // Arithmetic and rounding
            ("abs", "abs(x): absolute value."),
            ("abs2", "abs2(x): squared absolute value."),
            ("sign", "sign(x): -1, 0 or 1."),
            ("sqrt", "sqrt(x): square root."),
            ("cbrt", "cbrt(x): cube root."),
            ("exp", "exp(x): eˣ."),
            ("exp2", "exp2(x): 2ˣ."),
            ("exp10", "exp10(x): 10ˣ."),
            ("expm1", "expm1(x): eˣ - 1, accurate near 0."),
            ("log", "log(x), log(b, x): natural logarithm, or to base b."),
            ("log2", "log2(x): base-2 logarithm."),
            ("log10", "log10(x): base-10 logarithm."),
            ("log1p", "log1p(x): log(1 + x), accurate near 0."),
            ("hypot", "hypot(x, y): √(x² + y²) without overflow."),
            ("div", "div(x, y): x ÷ y, truncated."),
            ("rem", "rem(x, y): remainder of div, sign of x."),
            ("mod", "mod(x, y): modulus, sign of y."),
            ("fld", "fld(x, y): x ÷ y, rounded down."),
            ("cld", "cld(x, y): x ÷ y, rounded up."),
            ("gcd", "gcd(a, b): greatest common divisor."),
            ("lcm", "lcm(a, b): least common multiple."),
            ("floor", "floor(x), floor(T, x): round down."),
            ("ceil", "ceil(x), ceil(T, x): round up."),
            ("round", "round(x; digits=n), round(T, x): round to nearest."),
            ("trunc", "trunc(x), trunc(T, x): round towards zero."),
            ("clamp", "clamp(x, lo, hi): x limited to [lo, hi]."),
            ("min", "min(a, b, …): the smallest argument."),
            ("max", "max(a, b, …): the largest argument."),
            ("minmax", "minmax(a, b): (min, max)."),
            ("muladd", "muladd(x, y, z): x*y + z, possibly fused."),
            ("float", "float(x): x as a floating-point number."),
            ("Float64", "Float64(x): x as a 64-bit float."),
            ("Int", "Int(x): x as a machine integer (exact, or an error)."),
            ("convert", "convert(T, x): x converted to type T."),
            ("oftype", "oftype(x, y): y converted to the type of x."),
            ("zero", "zero(x), zero(T): the additive identity."),
            ("one", "one(x), one(T): the multiplicative identity."),
            ("iszero", "iszero(x): whether x == 0."),
            ("isnan", "isnan(x): whether x is NaN."),
            ("isinf", "isinf(x): whether x is ±Inf."),
            ("isfinite", "isfinite(x): neither Inf nor NaN."),
            ("isapprox", "isapprox(x, y; rtol, atol): x ≈ y."),
            ("iseven", "iseven(n): whether n is even."),
            ("isodd", "isodd(n): whether n is odd."),

            // Trigonometry
            ("sin", "sin(x): sine, x in radians."),
            ("cos", "cos(x): cosine, x in radians."),
            ("tan", "tan(x): tangent, x in radians."),
            ("sincos", "sincos(x): (sin(x), cos(x))."),
            ("asin", "asin(x): inverse sine."),
            ("acos", "acos(x): inverse cosine."),
            ("atan", "atan(y), atan(y, x): inverse tangent."),
            ("sinh", "sinh(x): hyperbolic sine."),
            ("cosh", "cosh(x): hyperbolic cosine."),
            ("tanh", "tanh(x): hyperbolic tangent."),
            ("sind", "sind(x): sine, x in degrees."),
            ("cosd", "cosd(x): cosine, x in degrees."),
            ("deg2rad", "deg2rad(x): degrees to radians."),
            ("rad2deg", "rad2deg(x): radians to degrees."),

            // Complex numbers
            ("real", "real(z): real part."),
            ("imag", "imag(z): imaginary part."),
            ("conj", "conj(z): complex conjugate."),
            ("angle", "angle(z): argument of z, in radians."),
            ("complex", "complex(re, im): a complex number."),
            ("cis", "cis(θ): exp(iθ)."),

            // Building arrays
            ("zeros", "zeros(T, dims…): an array of zeros."),
            ("ones", "ones(T, dims…): an array of ones."),
            ("fill", "fill(value, dims…): an array of one value."),
            ("rand", "rand(T, dims…): uniformly random numbers."),
            ("randn", "randn(T, dims…): normally distributed random numbers."),
            ("range", "range(start, stop; length, step): an evenly spaced range."),
            ("collect", "collect(itr): an array of the elements."),
            ("similar", "similar(A, T, dims): an uninitialised array like A."),
            ("copy", "copy(A): a shallow copy."),
            ("deepcopy", "deepcopy(x): a full copy."),
            ("reshape", "reshape(A, dims…): the same elements in another shape."),
            ("vec", "vec(A): A as a vector."),
            ("hcat", "hcat(A, B, …): side by side."),
            ("vcat", "vcat(A, B, …): one above the other."),
            ("cat", "cat(A, B; dims): concatenated along dims."),
            ("repeat", "repeat(A, counts…): A tiled."),
            ("transpose", "transpose(A): rows become columns."),
            ("inv", "inv(A): inverse, of a number or a matrix (prefer A \\ b to solve)."),
            ("kron", "kron(A, B): Kronecker product."),
            ("permutedims", "permutedims(A): transpose that isn't recursive."),
            ("reverse", "reverse(v): elements in reverse order."),
            ("diff", "diff(v): differences of neighbours."),
            ("cumsum", "cumsum(v): running sum."),
            ("cumprod", "cumprod(v): running product."),

            // Looking at arrays
            ("length", "length(A): number of elements."),
            ("size", "size(A), size(A, d): dimensions."),
            ("ndims", "ndims(A): number of dimensions."),
            ("axes", "axes(A), axes(A, d): valid indices."),
            ("eachindex", "eachindex(A): efficient indices of A."),
            ("eachrow", "eachrow(M): the rows of M."),
            ("eachcol", "eachcol(M): the columns of M."),
            ("eltype", "eltype(A): type of the elements."),
            ("isempty", "isempty(A): whether A has no elements."),
            ("first", "first(A), first(A, n): the first element(s)."),
            ("last", "last(A), last(A, n): the last element(s)."),
            ("firstindex", "firstindex(A): the first index."),
            ("lastindex", "lastindex(A): the last index."),

            // Reductions and searches
            ("sum", "sum(A; dims), sum(f, A): sum of the elements."),
            ("prod", "prod(A; dims), prod(f, A): product of the elements."),
            ("minimum", "minimum(A; dims): the smallest element."),
            ("maximum", "maximum(A; dims): the largest element."),
            ("extrema", "extrema(A): (minimum, maximum)."),
            ("argmin", "argmin(A): index of the smallest element."),
            ("argmax", "argmax(A): index of the largest element."),
            ("findmin", "findmin(A): (smallest, its index)."),
            ("findmax", "findmax(A): (largest, its index)."),
            ("findall", "findall(f, A): indices where f is true."),
            ("findfirst", "findfirst(f, A): first index where f is true."),
            ("findlast", "findlast(f, A): last index where f is true."),
            ("count", "count(f, A): how many elements f is true for."),
            ("any", "any(f, A): whether f is true for some element."),
            ("all", "all(f, A): whether f is true for every element."),
            ("sort", "sort(v; by, rev): a sorted copy."),
            ("sortperm", "sortperm(v): the permutation that sorts v."),
            ("unique", "unique(v): the elements without duplicates."),

            // Functions over collections
            ("map", "map(f, A, …): f applied to each element."),
            ("filter", "filter(f, A): the elements f is true for."),
            ("reduce", "reduce(op, A; init): A combined with op."),
            ("foldl", "foldl(op, A; init): reduce, left to right."),
            ("mapreduce", "mapreduce(f, op, A): map then reduce, without the temporary."),
            ("foreach", "foreach(f, A): calls f on each element."),
            ("enumerate", "enumerate(A): (index, element) pairs."),
            ("zip", "zip(A, B, …): elements side by side."),
            ("broadcast", "broadcast(f, A, …): f.(A, …)."),

            // Changing arrays
            ("push!", "push!(v, x…): adds to the end."),
            ("pop!", "pop!(v): removes and returns the last element."),
            ("append!", "append!(v, w): adds w's elements to the end."),
            ("pushfirst!", "pushfirst!(v, x…): adds to the front."),
            ("insert!", "insert!(v, i, x): inserts x at index i."),
            ("deleteat!", "deleteat!(v, i): removes the element at i."),
            ("fill!", "fill!(A, x): sets every element to x."),
            ("sort!", "sort!(v; by, rev): sorts in place."),
            ("reverse!", "reverse!(v): reverses in place."),
            ("resize!", "resize!(v, n): changes the length."),

            // Strings and output
            ("string", "string(xs…): the values written as text."),
            ("repr", "repr(x): x as Julia would show it."),
            ("println", "println(xs…): prints, then a new line."),
            ("print", "print(xs…): prints."),
            ("show", "show(x): shows x."),
            ("typeof", "typeof(x): the type of x."),
            ("error", "error(msg…): throws an ErrorException."),
            ("throw", "throw(e): throws an exception."),
            ("ArgumentError", "ArgumentError(msg): an invalid argument."),
            ("DomainError", "DomainError(x, msg): x is outside the domain."),
        }.Select(b => new JuliaCompletion(b.Item1, b.Item2, CompletionKind.Builtin)).ToArray();

        /// <summary>
        /// Everything that could be suggested in <paramref name="function"/>'s body, in order; the
        /// first of equal names wins (a parameter named like a Base function is the parameter).
        /// </summary>
        /// <param name="others">The other functions of the design, which the body can call.</param>
        public static IReadOnlyList<JuliaCompletion> All(Function function, IEnumerable<Function> others)
        {
            IEnumerable<JuliaCompletion> parameters = function.Parameters
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .Select(p => new JuliaCompletion(p.Name, $"Parameter {p.Signature}", CompletionKind.Parameter));

            IEnumerable<JuliaCompletion> functions = others
                .Append(function)
                .Where(f => f.Error is null)
                .Select(f => new JuliaCompletion(f.Name, f.Signature, CompletionKind.Function));

            HashSet<string> loaded = function.LoadedPackages.ToHashSet();
            IEnumerable<JuliaCompletion> library = JuliaStdlib.Functions.Select(f => loaded.Contains(f.Package)
                ? new JuliaCompletion(f.Name, $"{f.Package}: {f.Detail}", CompletionKind.Builtin)
                : new JuliaCompletion(f.Name, $"{f.Package}: {f.Detail} Picking it adds {f.Package} to Packages.", CompletionKind.Builtin, f.Package));

            return parameters
                .Concat(functions)
                .Concat(Keywords)
                .Concat(Constants)
                .Concat(Builtins)
                .Concat(library)
                .DistinctBy(c => c.Text)
                .ToList();
        }

        /// <summary>
        /// Suggestions starting with <paramref name="prefix"/>, ignoring case, exact-case matches first
        /// and otherwise in the order of <see cref="All"/>.
        /// </summary>
        public static IReadOnlyList<JuliaCompletion> Matching(IEnumerable<JuliaCompletion> all, string prefix)
        {
            return all
                .Where(c => c.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Text.StartsWith(prefix, StringComparison.Ordinal) ? 0 : 1)
                .ToList();
        }

        /// <summary>
        /// The part of a name just before <paramref name="caret"/>, to complete; null when the caret
        /// isn't in code where a name could be (inside a comment, a string, or a number).
        /// </summary>
        public static string? WordBefore(string text, int caret)
        {
            int start = caret;
            while (start > 0 && IsNamePart(text[start - 1]))
            {
                start--;
            }

            // A number like 2pi or 1e5 isn't a name.
            if (start < caret && char.IsAsciiDigit(text[start]))
            {
                return null;
            }

            int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
            return InCommentOrString(text[lineStart..start]) ? null : text[start..caret];
        }

        /// <summary>
        /// Letters, digits, underscores and the ! of mutating functions.
        /// </summary>
        public static bool IsNamePart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '!';
        }

        /// <summary>
        /// Whether the end of <paramref name="line"/> is inside a # comment or a "string" (escaped
        /// quotes and 'c' characters aside; multi-line strings and #= =# comments aren't tracked).
        /// </summary>
        private static bool InCommentOrString(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                }
                else if (c == '"')
                {
                    inString = true;
                }
                else if (c == '#')
                {
                    return true;
                }
                else if (c == '\'' && i + 2 < line.Length && line[i + 1] != '\\' && line[i + 2] == '\'')
                {
                    // A character literal like '#' or '"'; a ' after a name is the adjoint.
                    i += 2;
                }
                else if (c == '\'' && i + 3 < line.Length && line[i + 1] == '\\' && line[i + 3] == '\'')
                {
                    // An escaped one like '\'' or '\n'.
                    i += 3;
                }
            }

            return inString;
        }
    }
}
