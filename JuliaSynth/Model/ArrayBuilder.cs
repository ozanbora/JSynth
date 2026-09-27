using Avalonia.Media;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// An input of an <see cref="ArrayBuilder"/>, e.g. the number of rows: wired, or else the value typed in.
    /// </summary>
    public class Argument : Parameter
    {
        private string _value = string.Empty;

        /// <summary>
        /// The value used while no wire feeds the argument, as typed.
        /// </summary>
        [JsonProperty]
        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value ?? string.Empty);
        }

        /// <summary>
        /// Whether a value can be typed in; some arguments (a vector) always need a wire.
        /// </summary>
        public bool HasValue => Key != "v";

        /// <summary>
        /// Which argument of the builder this is; also its name.
        /// </summary>
        public string Key => Name;

        /// <summary>
        /// Shown on the node: "n = 100", or the signature while a wire feeds it.
        /// </summary>
        public string Label => IsWired || !HasValue ? Signature : $"{Name} = {Value}";

        public Argument() : this("x", Function.AnyType, string.Empty)
        {
        }

        public Argument(string key, string type, string value) : base(key, type)
        {
            _value = value;
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(Value) or nameof(IsWired) or nameof(Signature))
                {
                    this.RaisePropertyChanged(nameof(Label));
                }
            };
        }
    }

    /// <summary>
    /// A vector or matrix made by a Julia function (zeros, rand, spzeros, ...) instead of from values typed
    /// in, so it can be any size: only the recipe is saved. Sizes and other arguments can be typed in or wired.
    /// </summary>
    public class ArrayBuilder : Node
    {
        /// <summary>
        /// What a kind of builder makes and needs.
        /// </summary>
        /// <param name="Dimensions">1 for vectors only, 2 for matrices only, 0 for either.</param>
        private sealed record Recipe(string Kind, bool Sparse, int Dimensions, bool FloatsOnly, string? Package, string Description);

        private static readonly IReadOnlyList<Recipe> Recipes = [
            new("zeros", false, 0, false, null, "All zeros."),
            new("ones", false, 0, false, null, "All ones."),
            new("fill", false, 0, false, null, "Every element the same value."),
            new("rand", false, 0, false, null, "Uniformly random values: in [0, 1) for floats, any value for integers."),
            new("randn", false, 0, true, null, "Normally distributed random values (mean 0, standard deviation 1)."),
            new("range", false, 1, true, null, "n evenly spaced values from start to stop."),
            new("identity", false, 2, false, "LinearAlgebra", "The n×n identity matrix."),
            new("diagm", false, 2, false, "LinearAlgebra", "A matrix with the vector v on its diagonal, zeros elsewhere."),
            new("spzeros", true, 0, false, "SparseArrays", "A sparse array without stored values."),
            new("sprand", true, 0, false, "SparseArrays", "A sparse array where each element is stored with the given probability, with a uniformly random value."),
            new("sprandn", true, 0, true, "SparseArrays", "Like sprand, with normally distributed values."),
            new("spidentity", true, 2, false, "SparseArrays", "The n×n identity matrix, sparse."),
            new("spdiagm", true, 2, false, "SparseArrays", "A sparse matrix with the vector v on its diagonal."),
        ];

        public static IReadOnlyList<string> Kinds { get; } = Recipes.Select(r => r.Kind).ToList();

        /// <summary>
        /// The shapes a builder can make; whether it is sparse depends on the kind.
        /// </summary>
        public static IReadOnlyList<string> Shapes { get; } = [JuliaTypes.Vector, JuliaTypes.Matrix];

        #region Private Properties
        private string _kind = "zeros";
        private string _shape = JuliaTypes.Vector;
        private string _elementType = "Float64";
        private Dictionary<string, Argument> _arguments = [];
        private string? _error;
        private bool _updating;
        private bool _updateAgain;
        #endregion

        #region Public Properties
        [JsonProperty]
        public string Kind
        {
            get => _kind;
            // Ignore the null a ComboBox writes back when its selection is cleared, and unknown kinds.
            set
            {
                if (value is not null && Kinds.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _kind, value);
                    Update();
                }
            }
        }

        /// <summary>
        /// Vector or Matrix; fixed by kinds that only make one of them.
        /// </summary>
        [JsonProperty]
        public string Shape
        {
            get => _shape;
            set
            {
                if (value is not null && Shapes.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _shape, value);
                    Update();
                }
            }
        }

        [JsonProperty]
        public string ElementType
        {
            get => _elementType;
            set
            {
                if (value is not null && JuliaTypes.Numeric.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _elementType, value);
                    Update();
                }
            }
        }

        /// <summary>
        /// Every argument, including those the current kind doesn't use, so switching kinds keeps what was typed.
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<Argument> Arguments
        {
            get => [.. _arguments.Values];
            set
            {
                foreach (Argument argument in _arguments.Values)
                {
                    argument.PropertyChanged -= ArgumentChanged;
                }

                // Start from the defaults, so a file from before an argument existed still has it.
                _arguments = DefaultArguments();
                foreach (Argument argument in value ?? [])
                {
                    _arguments[argument.Key] = argument;
                }

                foreach (Argument argument in _arguments.Values)
                {
                    argument.PropertyChanged += ArgumentChanged;
                }

                Update();
            }
        }

        /// <summary>
        /// The arguments the current kind takes, in order.
        /// </summary>
        public ObservableCollection<Parameter> Parameters { get; } = [];

        public override IReadOnlyList<Parameter> Inputs => Parameters;

        public override IEnumerable<Parameter> AllInputs => _arguments.Values;

        public IReadOnlyList<string> PossibleTypes => Current.FloatsOnly ? JuliaTypes.Floats : JuliaTypes.Numeric;

        public bool IsSparse => Current.Sparse;

        /// <summary>
        /// Whether the kind makes both vectors and matrices.
        /// </summary>
        public bool CanPickShape => Current.Dimensions == 0;

        public bool IsVector => Shape == JuliaTypes.Vector;

        public string Description => Current.Description;

        /// <summary>
        /// The math symbol shown on the node: bold v for a vector, bold A for a matrix.
        /// </summary>
        public string Symbol => IsVector ? "𝒗" : "𝑨";

        public override string OutputType => JuliaTypes.Compose(OutputShape, ElementType);

        private string OutputShape => (IsSparse, IsVector) switch
        {
            (true, true) => JuliaTypes.SparseVector,
            (true, false) => JuliaTypes.SparseMatrix,
            (false, true) => JuliaTypes.Vector,
            (false, false) => JuliaTypes.Matrix,
        };

        /// <summary>
        /// "1000-element", "100×100, sparse 1%", with argument names for wired sizes.
        /// </summary>
        public string Summary
        {
            get
            {
                string size = Kind switch
                {
                    "diagm" or "spdiagm" => "square",
                    "identity" or "spidentity" => $"{Size("n")}×{Size("n")}",
                    _ when IsVector => $"{Size("n")}-element",
                    _ => $"{Size("m")}×{Size("n")}",
                };

                string storage = !IsSparse ? string.Empty
                    : Kind is "sprand" or "sprandn" && !Arg("density").IsWired && Density() is double p ? $", sparse {p:0.###%}"
                    : ", sparse";
                return size + storage;
            }
        }

        /// <summary>
        /// The Julia expression making the array, e.g. <c>zeros(Float64, 100, 100)</c>.
        /// </summary>
        public string Julia
        {
            get
            {
                string t = ElementType;
                string dims = IsVector ? Use("n") : $"{Use("m")}, {Use("n")}";
                return Kind switch
                {
                    "fill" => $"fill({UseAs("value")}, {dims})",
                    "range" => $"collect(range({UseAs("start")}, {UseAs("stop")}; length = {Use("n")}))",
                    "identity" => $"Matrix{{{t}}}(I, {Use("n")}, {Use("n")})",
                    "diagm" => $"diagm({Use("v")})",
                    "sprand" or "sprandn" => $"{Kind}({t}, {dims}, {Use("density")})",
                    "spidentity" => $"spdiagm(ones({t}, {Use("n")}))",
                    "spdiagm" => $"spdiagm({Use("v")})",
                    _ => $"{Kind}({t}, {dims})",
                };
            }
        }

        /// <summary>
        /// <see cref="Julia"/>, after the <c>using</c> it needs.
        /// </summary>
        public string Code => Package is string package ? $"using {package}\n{Julia}" : Julia;

        /// <summary>
        /// The package the Julia code needs loaded, or null.
        /// </summary>
        public string? Package => Current.Package;

        /// <summary>
        /// Why the arguments aren't valid, or null.
        /// </summary>
        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }
        #endregion

        public ArrayBuilder()
        {
            Color = Colors.SeaGreen;
            Arguments = [];
        }

        private Recipe Current => Recipes.First(r => r.Kind == Kind);

        private Argument Arg(string key) => _arguments[key];

        private static Dictionary<string, Argument> DefaultArguments()
        {
            return new Argument[]
            {
                new("m", "Int64", "100"),
                new("n", "Int64", "100"),
                new("value", "Float64", "1"),
                new("start", "Float64", "0"),
                new("stop", "Float64", "1"),
                new("density", "Float64", "0.01"),
                new("v", "Vector{Float64}", string.Empty),
            }.ToDictionary(a => a.Key);
        }

        /// <summary>
        /// The keys of the arguments the current kind takes, in the order Julia takes them.
        /// </summary>
        private IEnumerable<string> Keys()
        {
            IEnumerable<string> dims = IsVector ? ["n"] : ["m", "n"];
            return Kind switch
            {
                "fill" => ["value", .. dims],
                "range" => ["start", "stop", "n"],
                "identity" or "spidentity" => ["n"],
                "diagm" or "spdiagm" => ["v"],
                "sprand" or "sprandn" => [.. dims, "density"],
                _ => dims,
            };
        }

        /// <summary>
        /// The argument in Julia: its name when wired (it is then a variable), else the value typed in.
        /// </summary>
        private string Use(string key)
        {
            Argument argument = Arg(key);
            return argument.IsWired || !argument.HasValue ? argument.Name : argument.Value.Trim();
        }

        /// <summary>
        /// Like <see cref="Use"/>, converting a typed-in value to the element type: <c>Float32(0.5)</c>.
        /// </summary>
        private string UseAs(string key)
        {
            return Arg(key).IsWired ? Use(key) : $"{ElementType}({Use(key)})";
        }

        private string Size(string key)
        {
            return Arg(key).IsWired ? key : Use(key);
        }

        private double? Density()
        {
            return double.TryParse(Arg("density").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ? p : null;
        }

        private void ArgumentChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Argument.Value) or nameof(Parameter.IsWired))
            {
                Update();
            }
        }

        private void Update()
        {
            // The constructor sets the arguments last; nothing to do before that.
            if (_arguments.Count == 0)
            {
                return;
            }

            // Removing an argument drops its wire, which updates the wiring state, which asks for another
            // update: do that one after this one, not while the parameter list is being changed.
            if (_updating)
            {
                _updateAgain = true;
                return;
            }

            _updating = true;
            try
            {
                do
                {
                    _updateAgain = false;
                    UpdateOnce();
                }
                while (_updateAgain);
            }
            finally
            {
                _updating = false;
            }
        }

        private void UpdateOnce()
        {
            Recipe recipe = Current;
            if (recipe.Dimensions != 0)
            {
                _shape = recipe.Dimensions == 1 ? JuliaTypes.Vector : JuliaTypes.Matrix;
            }

            if (!PossibleTypes.Contains(_elementType))
            {
                _elementType = "Float64";
            }

            foreach (string key in new[] { "value", "start", "stop" })
            {
                Arg(key).Type = ElementType;
            }

            // A dense diagonal matrix is made from a dense vector; spdiagm takes any vector.
            Arg("v").Type = JuliaTypes.Compose(recipe.Sparse ? JuliaTypes.AbstractVector : JuliaTypes.Vector, ElementType);

            SyncParameters([.. Keys().Select(Arg)]);
            Error = Validate();

            foreach (string name in new[]
            {
                nameof(Shape), nameof(ElementType), nameof(PossibleTypes), nameof(IsSparse), nameof(CanPickShape),
                nameof(IsVector), nameof(Description), nameof(Symbol), nameof(OutputType), nameof(Summary),
                nameof(Julia), nameof(Code),
            })
            {
                this.RaisePropertyChanged(name);
            }
        }

        /// <summary>
        /// Makes <see cref="Parameters"/> hold <paramref name="wanted"/>, touching only what changes, so the
        /// wires on the arguments that stay aren't dropped.
        /// </summary>
        private void SyncParameters(IReadOnlyList<Argument> wanted)
        {
            foreach (Parameter parameter in Parameters.Where(p => !wanted.Contains(p)).ToList())
            {
                _ = Parameters.Remove(parameter);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                int index = Parameters.IndexOf(wanted[i]);
                if (index < 0)
                {
                    Parameters.Insert(i, wanted[i]);
                }
                else if (index != i)
                {
                    Parameters.Move(index, i);
                }
            }
        }

        private string? Validate()
        {
            foreach (Argument argument in Parameters.OfType<Argument>().Where(a => !a.IsWired))
            {
                string value = argument.Value.Trim();
                string? error = argument.Key switch
                {
                    "v" => "Wire a vector into v.",
                    "m" or "n" when !JuliaTypes.IsValidLiteral("Int64", value) || value.StartsWith('-') => $"{argument.Key} must be a whole number, 0 or more.",
                    "density" when Density() is not (>= 0 and <= 1) => "density must be a number from 0 to 1.",
                    "value" or "start" or "stop" when !JuliaTypes.IsValidLiteral(ElementType, value) => $"{argument.Key}: '{value}' is not a valid {ElementType}.",
                    _ => null,
                };

                if (error is not null)
                {
                    return error;
                }
            }

            return null;
        }
    }
}
