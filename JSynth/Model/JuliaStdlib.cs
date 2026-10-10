using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
{
    /// <summary>
    /// A function of a standard library, with a line about it.
    /// </summary>
    public sealed record StdlibFunction(string Name, string Package, string Detail);

    /// <summary>
    /// Names a function body uses from a package it doesn't load.
    /// </summary>
    public sealed record PackageHint(string Package, IReadOnlyList<string> Names)
    {
        /// <summary>
        /// The names as a list: "mean, std".
        /// </summary>
        public string Uses => string.Join(", ", Names);

        public string Verb => Names.Count == 1 ? "is in" : "are in";
    }

    /// <summary>
    /// Common functions of Julia's standard libraries: packages that come with Julia, so adding one
    /// never needs an install.
    /// </summary>
    public static class JuliaStdlib
    {
        public static IReadOnlyList<StdlibFunction> Functions { get; } = new (string, string, string)[]
        {
            ("LinearAlgebra", "norm", "norm(A, p=2): length of a vector, or a matrix norm."),
            ("LinearAlgebra", "opnorm", "opnorm(A, p=2): operator norm of a matrix."),
            ("LinearAlgebra", "normalize", "normalize(v): v scaled to norm 1."),
            ("LinearAlgebra", "dot", "dot(x, y): dot product, x ⋅ y."),
            ("LinearAlgebra", "cross", "cross(x, y): cross product of 3-vectors."),
            ("LinearAlgebra", "det", "det(A): determinant."),
            ("LinearAlgebra", "logdet", "logdet(A): log of the determinant."),
            ("LinearAlgebra", "pinv", "pinv(A): Moore–Penrose pseudo-inverse."),
            ("LinearAlgebra", "tr", "tr(A): trace, the sum of the diagonal."),
            ("LinearAlgebra", "rank", "rank(A): rank of a matrix."),
            ("LinearAlgebra", "diag", "diag(A, k=0): the k-th diagonal as a vector."),
            ("LinearAlgebra", "diagm", "diagm(v): a matrix with v on the diagonal."),
            ("LinearAlgebra", "Diagonal", "Diagonal(v): a diagonal matrix."),
            ("LinearAlgebra", "I", "I: the identity, sized to fit (A + I, Matrix(I, n, n))."),
            ("LinearAlgebra", "eigen", "eigen(A): eigenvalues and eigenvectors (.values, .vectors)."),
            ("LinearAlgebra", "eigvals", "eigvals(A): eigenvalues."),
            ("LinearAlgebra", "eigvecs", "eigvecs(A): eigenvectors, as columns."),
            ("LinearAlgebra", "svd", "svd(A): singular value decomposition (.U, .S, .Vt)."),
            ("LinearAlgebra", "svdvals", "svdvals(A): singular values."),
            ("LinearAlgebra", "qr", "qr(A): QR factorisation."),
            ("LinearAlgebra", "lu", "lu(A): LU factorisation."),
            ("LinearAlgebra", "cholesky", "cholesky(A): Cholesky factorisation of a positive definite matrix."),
            ("LinearAlgebra", "nullspace", "nullspace(A): a basis of the null space."),
            ("LinearAlgebra", "Symmetric", "Symmetric(A): A seen as symmetric."),
            ("LinearAlgebra", "issymmetric", "issymmetric(A): whether A == transpose(A)."),
            ("LinearAlgebra", "triu", "triu(A, k=0): the upper triangle."),
            ("LinearAlgebra", "tril", "tril(A, k=0): the lower triangle."),

            ("Statistics", "mean", "mean(v; dims): arithmetic mean."),
            ("Statistics", "median", "median(v): middle value."),
            ("Statistics", "std", "std(v; corrected=true): standard deviation."),
            ("Statistics", "var", "var(v; corrected=true): variance."),
            ("Statistics", "cor", "cor(x, y), cor(M): Pearson correlation."),
            ("Statistics", "cov", "cov(x, y), cov(M): covariance."),
            ("Statistics", "quantile", "quantile(v, p): the p-quantile, p in [0, 1]."),
            ("Statistics", "middle", "middle(v): the mean of the minimum and maximum."),

            ("SparseArrays", "sparse", "sparse(A), sparse(I, J, V, m, n): a sparse matrix."),
            ("SparseArrays", "spzeros", "spzeros(T, m, n): an empty sparse matrix."),
            ("SparseArrays", "sprand", "sprand(m, n, p): a random sparse matrix with density p."),
            ("SparseArrays", "spdiagm", "spdiagm(k => v, …): a sparse matrix from diagonals."),
            ("SparseArrays", "nnz", "nnz(S): number of stored entries."),
            ("SparseArrays", "nonzeros", "nonzeros(S): the stored values."),
            ("SparseArrays", "findnz", "findnz(S): (rows, columns, values) of the stored entries."),
            ("SparseArrays", "rowvals", "rowvals(S): the row of each stored value."),
            ("SparseArrays", "nzrange", "nzrange(S, j): where column j's values are stored."),
            ("SparseArrays", "issparse", "issparse(A): whether A is sparse."),
            ("SparseArrays", "dropzeros", "dropzeros(S): S without stored zeros."),

            ("Random", "shuffle", "shuffle(v): the elements in random order."),
            ("Random", "randperm", "randperm(n): a random permutation of 1:n."),
            ("Random", "randsubseq", "randsubseq(v, p): each element kept with probability p."),
        }.Select(f => new StdlibFunction(f.Item2, f.Item1, f.Item3)).ToList();

        /// <summary>
        /// The standard libraries above, by name.
        /// </summary>
        public static IReadOnlySet<string> Packages { get; } = Functions.Select(f => f.Package).ToHashSet();

        private static readonly ILookup<string, StdlibFunction> ByName = Functions.ToLookup(f => f.Name);

        /// <summary>
        /// Names <paramref name="body"/> uses from a library not in <paramref name="loaded"/>: known
        /// functions (unless the body or <paramref name="ownNames"/> define one of that name) and
        /// qualified names like <c>LinearAlgebra.norm</c>. One hint per package, in the order first used.
        /// </summary>
        public static IReadOnlyList<PackageHint> Hints(string body, IEnumerable<string> loaded, IEnumerable<string> ownNames)
        {
            HashSet<string> have = loaded.ToHashSet();
            HashSet<string> defined = ownNames.ToHashSet();
            List<(string Name, char Before, char After)> names = Names(body).ToList();

            // Assigned in the body (mean = sum(v) / n): the body's own.
            foreach ((string name, _, char after) in names)
            {
                _ = after == '=' ? defined.Add(name) : false;
            }

            List<(string Name, string Package)> hints = [];
            for (int i = 0; i < names.Count; i++)
            {
                (string name, char before, char after) = names[i];
                if (before == '.' || defined.Contains(name))
                {
                    continue;
                }

                (string Name, string Package)? hint =
                    after == '.' && Packages.Contains(name) && !have.Contains(name)
                        ? (i + 1 < names.Count && names[i + 1].Before == '.' ? $"{name}.{names[i + 1].Name}" : name, name)
                    : after != '.' && ByName[name].FirstOrDefault() is StdlibFunction known && !have.Contains(known.Package)
                        ? (name, known.Package)
                    : null;

                if (hint is { } found && !hints.Contains(found))
                {
                    hints.Add(found);
                }
            }

            return hints
                .GroupBy(h => h.Package)
                .Select(g => new PackageHint(g.Key, g.Select(h => h.Name).ToList()))
                .ToList();
        }

        /// <summary>
        /// The names in <paramref name="code"/> outside strings and comments, with the characters just
        /// before and after them (spaces skipped after; '=' only for an assignment, not ==).
        /// </summary>
        private static IEnumerable<(string Name, char Before, char After)> Names(string code)
        {
            int i = 0;
            while (i < code.Length)
            {
                char c = code[i];
                if (c == '#')
                {
                    while (i < code.Length && code[i] != '\n')
                    {
                        i++;
                    }
                }
                else if (c == '"')
                {
                    for (i++; i < code.Length && code[i] != '"'; i++)
                    {
                        i += code[i] == '\\' ? 1 : 0;
                    }

                    i++;
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < code.Length && JuliaCompletions.IsNamePart(code[i]))
                    {
                        i++;
                    }

                    int next = i;
                    while (next < code.Length && code[next] is ' ' or '\t')
                    {
                        next++;
                    }

                    char before = start > 0 ? code[start - 1] : ' ';
                    char after = next < code.Length ? code[next] : ' ';
                    if (after == '=' && next + 1 < code.Length && code[next + 1] is '=' or '>')
                    {
                        after = ' ';
                    }

                    yield return (code[start..i], before, after);
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
