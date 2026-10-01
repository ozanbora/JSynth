using Avalonia.Media;
using JuliaSynth.Runtime;
using Newtonsoft.Json;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A node at the end of a design: it takes one value and does something with it, and has no output.
    /// </summary>
    public abstract class OutputNode : Node
    {
        /// <summary>
        /// The one input. Saved, so wires to it survive saving and undo.
        /// </summary>
        [JsonProperty]
        public Parameter Input { get; private set; } = new("value", Function.AnyType);

        public override IReadOnlyList<Parameter> Inputs => [Input];

        public override bool HasOutput => false;

        /// <summary>
        /// Why nothing can be done yet, e.g. no file picked; null when ready.
        /// </summary>
        public virtual string? Problem => null;

        /// <summary>
        /// Julia code doing the node's work with the value in <paramref name="input"/>; its value is reported.
        /// </summary>
        public abstract string Julia(string input);

        /// <summary>
        /// The same as a plain statement, for an exported script.
        /// </summary>
        public abstract string Statement(string input);

        /// <summary>
        /// The packages the code needs.
        /// </summary>
        public virtual IEnumerable<string> Packages => [];
    }

    /// <summary>
    /// Shows a value on the canvas: the value it had in the last run, until the design changes.
    /// </summary>
    public class DisplayNode : OutputNode
    {
        private static readonly string[] ShownProperties = [nameof(Shown), nameof(ShownType), nameof(IsShownError)];

        public DisplayNode()
        {
            Color = NodePalette.Display;
        }

        /// <summary>
        /// The value as Julia shows it, what went wrong, or what to do to see it.
        /// </summary>
        public string Shown => Result switch
        {
            { Status: ReportStatus.Ok } r => r.Text,
            { Status: ReportStatus.Error } r => r.Text,
            { Status: ReportStatus.Skipped } r => $"Not run: {r.Text}",
            _ => "Run the design (F5) to see the value here.",
        };

        /// <summary>
        /// The value's Julia type, e.g. Vector{Float64}; empty before a run and after a failure.
        /// </summary>
        public string ShownType => Result?.Status == ReportStatus.Ok ? Result.Type : string.Empty;

        public bool IsShownError => Result?.Status is ReportStatus.Error or ReportStatus.Skipped;

        public override string Julia(string input) => input;

        public override string Statement(string input) => $"display({input})";

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || ShownProperties.Contains(property);
        }

        protected override void OnResultChanged()
        {
            foreach (string name in ShownProperties)
            {
                this.RaisePropertyChanged(name);
            }
        }
    }

    /// <summary>
    /// Writes a value to a file: CSV or TSV (any array), Matrix Market (sparse matrices) or JLD2 (anything),
    /// the formats Load from file reads. The file is overwritten on every run.
    /// </summary>
    public class SaveFile : OutputNode
    {
        #region Private Properties
        private string _path = string.Empty;
        private string _format = ArrayFile.Csv;
        private string _variable = "A";
        #endregion

        #region Public Properties
        /// <summary>
        /// Where to write; a relative path is relative to the design's folder.
        /// </summary>
        // Saved and read back in order: setting the path picks a format.
        [JsonProperty(Order = 1)]
        public string Path
        {
            get => _path;
            set
            {
                string path = (value ?? string.Empty).Trim().Trim('"');
                if (path == _path)
                {
                    return;
                }

                _path = path;
                this.RaisePropertyChanged();
                if (ArrayFile.FormatOf(path) is string format)
                {
                    _format = format;
                }

                Update();
            }
        }

        [JsonProperty(Order = 2)]
        public string Format
        {
            get => _format;
            set
            {
                if (value is not null && ArrayFile.Formats.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _format, value);
                    Update();
                }
            }
        }

        /// <summary>
        /// The name to store the value under in a JLD2 file.
        /// </summary>
        [JsonProperty(Order = 3)]
        public string Variable
        {
            get => _variable;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _variable, (value ?? string.Empty).Trim());
                Update();
            }
        }

        public bool IsJld2 => Format == ArrayFile.Jld2;

        public string FileName => _path.Length == 0 ? "no file" : System.IO.Path.GetFileName(_path);

        public override string? Problem => _path.Length == 0 ? "Pick a file to save to."
            : IsJld2 && !IsIdentifier(Variable) ? "Give the value a name to store it under."
            : null;

        /// <summary>
        /// Shown on the node when there's a problem.
        /// </summary>
        public string? Error => Problem;

        public override IEnumerable<string> Packages => Format switch
        {
            ArrayFile.Csv or ArrayFile.Tsv => ["DelimitedFiles"],
            ArrayFile.MatrixMarket => ["MatrixMarket", "SparseArrays"],
            _ => ["JLD2"],
        };

        /// <summary>
        /// The Julia code writing a value called <c>value</c>, for the edit panel.
        /// </summary>
        public string Code => $"using {string.Join(", ", Packages)}\n{Statement(Input.Name)}";
        #endregion

        public SaveFile()
        {
            Color = NodePalette.SaveFile;
            Update();
        }

        /// <summary>
        /// Writes the file, then reports where it went.
        /// </summary>
        public override string Julia(string input) => $"begin\n    {Statement(input)}\n    abspath({ArrayFile.PathLiteral(_path)})\nend";

        public override string Statement(string input)
        {
            string path = ArrayFile.PathLiteral(_path);
            return Format switch
            {
                ArrayFile.Csv => $"writedlm({path}, {input}, ',')",
                ArrayFile.Tsv => $"writedlm({path}, {input}, '\\t')",
                ArrayFile.MatrixMarket => $"MatrixMarket.mmwrite({path}, {input})",
                _ => $"JLD2.jldsave({path}; {Variable} = {input})",
            };
        }

        private void Update()
        {
            // Matrix Market files hold sparse matrices; the others take anything writedlm or JLD2 can write.
            Input.Type = Format == ArrayFile.MatrixMarket ? JuliaTypes.SparseMatrix : Function.AnyType;

            foreach (string name in new[] { nameof(Format), nameof(IsJld2), nameof(FileName), nameof(Problem), nameof(Error), nameof(Code) })
            {
                this.RaisePropertyChanged(name);
            }
        }

        private static bool IsIdentifier(string name)
        {
            return name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
    }
}
