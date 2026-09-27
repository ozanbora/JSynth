using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A Julia function: a name, typed parameters, a return type and a body.
    /// The generated Julia definition is available through <see cref="Code"/>.
    /// </summary>
    public class Function : Node
    {
        public const string AnyType = "Any";

        private static readonly Regex IdentifierRegex = new(@"^[\p{L}_][\p{L}\p{N}_!]*$");

        private static readonly HashSet<string> Keywords = [
            "baremodule", "begin", "break", "catch", "const", "continue", "do", "else", "elseif",
            "end", "export", "false", "finally", "for", "function", "global", "if", "import",
            "let", "local", "macro", "module", "quote", "return", "struct", "true", "try",
            "using", "while",
        ];

        #region Private Properties
        private string _name = "f";
        private string _returnType = AnyType;
        private string _body = string.Empty;
        private string _signature = string.Empty;
        private string _code = string.Empty;
        private string? _error;
        #endregion

        #region Public Properties
        [JsonProperty]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        [JsonProperty]
        public string ReturnType
        {
            get => _returnType;
            // Ignore the null a ComboBox writes back when its selection is cleared.
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _returnType, value ?? _returnType);
                this.RaisePropertyChanged(nameof(OutputType));
                this.RaisePropertyChanged(nameof(ReturnShape));
                this.RaisePropertyChanged(nameof(ReturnElementType));
            }
        }

        /// <summary>
        /// Scalar, Vector or Matrix: the first half of <see cref="ReturnType"/>, picked separately.
        /// </summary>
        public string ReturnShape
        {
            get => JuliaTypes.Split(ReturnType).Shape;
            set
            {
                if (value is not null)
                {
                    ReturnType = JuliaTypes.Compose(value, ReturnElementType);
                }
            }
        }

        /// <summary>
        /// Any, Float64, ...: the second half of <see cref="ReturnType"/>.
        /// </summary>
        public string ReturnElementType
        {
            get => JuliaTypes.Split(ReturnType).ElementType;
            set
            {
                if (value is not null)
                {
                    ReturnType = JuliaTypes.Compose(ReturnShape, value);
                }
            }
        }

        [JsonProperty]
        public string Body
        {
            get => _body;
            set => this.RaiseAndSetIfChanged(ref _body, value);
        }

        // Get-only on purpose: the constructor adds no parameters, so the deserializer
        // populating this existing collection doesn't produce duplicates.
        [JsonProperty]
        public ObservableCollection<Parameter> Parameters { get; } = [];

        public override string OutputType => ReturnType;

        public static IReadOnlyList<string> Shapes => JuliaTypes.Shapes;

        public static IReadOnlyList<string> ElementTypes => JuliaTypes.ElementTypes;

        /// <summary>
        /// Julia signature, e.g. <c>f(x::Float64, y)::Float64</c>.
        /// </summary>
        public string Signature
        {
            get => _signature;
            private set => this.RaiseAndSetIfChanged(ref _signature, value);
        }

        /// <summary>
        /// Full Julia function definition.
        /// </summary>
        public string Code
        {
            get => _code;
            private set => this.RaiseAndSetIfChanged(ref _code, value);
        }

        /// <summary>
        /// Validation error for the name and parameters, or null when the function is valid.
        /// </summary>
        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }
        #endregion

        public Function() : base()
        {
            Color = Colors.DarkOrchid;

            Parameters.CollectionChanged += ParametersChanged;
            _ = this.WhenAnyValue(x => x.Name, x => x.ReturnType, x => x.Body)
                .Subscribe(_ => Update());
        }

        public void AddParameter()
        {
            // Pick the first free single-letter name: x, y, z, a, b, ...
            string name = "xyzabcdefghijklmnopqrstuvw"
                .Select(c => c.ToString())
                .FirstOrDefault(n => Parameters.All(p => p.Name != n))
                ?? $"x{Parameters.Count + 1}";

            Parameters.Add(new Parameter(name, AnyType));
        }

        public void RemoveParameter(Parameter parameter)
        {
            _ = Parameters.Remove(parameter);
        }

        private void ParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (Parameter parameter in e.OldItems)
                {
                    parameter.PropertyChanged -= ParameterChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (Parameter parameter in e.NewItems)
                {
                    parameter.PropertyChanged += ParameterChanged;
                }
            }

            Update();
        }

        private void ParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            Update();
        }

        private void Update()
        {
            string parameters = string.Join(", ", Parameters.Select(p => p.Signature));
            string returnType = ReturnType == AnyType ? string.Empty : $"::{ReturnType}";
            Signature = $"{Name}({parameters}){returnType}";

            IEnumerable<string> lines = (Body ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.TrimEnd())
                .SkipWhile(string.IsNullOrEmpty)
                .Reverse()
                .SkipWhile(string.IsNullOrEmpty)
                .Reverse()
                .Select(line => line.Length == 0 ? line : "    " + line);

            string body = string.Join("\n", lines);
            Code = body.Length == 0
                ? $"function {Signature}\nend"
                : $"function {Signature}\n{body}\nend";

            Error = Validate();
        }

        private string? Validate()
        {
            if (!IsIdentifier(Name))
            {
                return $"'{Name}' is not a valid function name.";
            }

            foreach (Parameter parameter in Parameters)
            {
                if (!IsIdentifier(parameter.Name))
                {
                    return $"'{parameter.Name}' is not a valid parameter name.";
                }
            }

            string? duplicate = Parameters
                .GroupBy(p => p.Name)
                .FirstOrDefault(g => g.Count() > 1)
                ?.Key;

            return duplicate is null ? null : $"Parameter '{duplicate}' is defined more than once.";
        }

        private static bool IsIdentifier(string? name)
        {
            return !string.IsNullOrEmpty(name) && IdentifierRegex.IsMatch(name) && !Keywords.Contains(name);
        }
    }
}
