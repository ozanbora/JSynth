using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JuliaSynth.Model
{
    /// <summary>
    /// <c>using</c> and <c>import</c> statements, which Julia only allows outside functions: finding them
    /// in a function body, and the packages they (or a list of names) load.
    /// </summary>
    public static class JuliaImports
    {
        // A statement starts with the keyword and a space; using_count = 1 is an assignment.
        private static readonly Regex StatementRegex = new(@"^\s*(using|import)\s");

        private static readonly Regex NameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

        /// <summary>
        /// Modules every Julia session has; nothing to load or install.
        /// </summary>
        private static readonly HashSet<string> AlwaysLoaded = ["Base", "Core", "Main"];

        /// <summary>
        /// The <c>using</c>/<c>import</c> statements of <paramref name="body"/>, wherever they are, and the
        /// body without them. A statement ending in a comma goes on on the next line; comments are dropped.
        /// </summary>
        public static (IReadOnlyList<string> Statements, string Remaining) Split(string body)
        {
            List<string> statements = [];
            List<string> rest = [];
            string[] lines = body.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (!StatementRegex.IsMatch(lines[i]))
                {
                    rest.Add(lines[i]);
                    continue;
                }

                // Comments go: joined into one line, they'd hide the rest of it.
                string statement = WithoutComment(lines[i]).Trim();
                while (statement.EndsWith(',') && i + 1 < lines.Length)
                {
                    statement += " " + WithoutComment(lines[++i]).Trim();
                }

                statements.Add(statement);
            }

            return (statements, string.Join("\n", rest));
        }

        /// <summary>
        /// The packages <paramref name="statement"/> loads (<c>using A, B.C</c> → A, B;
        /// <c>import A: x as y</c> → A), and a problem with it, if any.
        /// </summary>
        public static (IReadOnlyList<string> Packages, string? Problem) PackagesIn(string statement)
        {
            string text = StatementRegex.Replace(WithoutComment(statement), string.Empty, 1).Trim();

            // using A: x, y names one package; the rest are what to take from it.
            int colon = text.IndexOf(':');
            IEnumerable<string> modules = colon >= 0 ? [text[..colon]] : text.Split(',');

            List<string> packages = [];
            foreach (string module in modules.Select(m => m.Trim()))
            {
                // import A as B, using A.B: the package is A.
                string name = module.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Split('.')[0] ?? string.Empty;
                if (module.StartsWith('.'))
                {
                    return ([], $"'{statement.Trim()}' loads a module of this script, which a function node can't have.");
                }

                if (!NameRegex.IsMatch(name))
                {
                    return ([], $"'{statement.Trim()}' is not a using or import statement Julia understands.");
                }

                if (!AlwaysLoaded.Contains(name))
                {
                    packages.Add(name);
                }
            }

            return (packages.Distinct().ToList(), null);
        }

        /// <summary>
        /// The package names in <paramref name="list"/>, separated by commas or spaces, and the first
        /// that isn't a package name, if any.
        /// </summary>
        public static (IReadOnlyList<string> Packages, string? Invalid) ParseList(string? list)
        {
            List<string> names = (list ?? string.Empty)
                .Split([',', ' ', '\t', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            string? invalid = names.FirstOrDefault(n => !NameRegex.IsMatch(n));
            return (names.Where(n => NameRegex.IsMatch(n) && !AlwaysLoaded.Contains(n)).ToList(), invalid);
        }

        private static string WithoutComment(string line)
        {
            int hash = line.IndexOf('#');
            return (hash >= 0 ? line[..hash] : line).TrimEnd();
        }
    }
}
