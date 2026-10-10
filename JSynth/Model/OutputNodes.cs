using Avalonia.Media;
using JSynth.Runtime;
using Newtonsoft.Json;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
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
    /// the formats Load from file reads. The file is overwritten on every run, or, going through files one at a
    /// time, gets a row, or a picture, per file (see <see cref="PerFile"/>).
    /// </summary>
    public class SaveFile : OutputNode
    {
        #region Private Properties
        private string _path = string.Empty;
        private string _format = ArrayFile.Csv;
        private string _nameInFile = "A";
        private bool _perFile;
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
        [JsonProperty("Variable", Order = 3)]
        public string NameInFile
        {
            get => _nameInFile;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _nameInFile, (value ?? string.Empty).Trim());
                Update();
            }
        }

        /// <summary>
        /// Saving per file: going through files one at a time, each file's value is added as a row, after the file's
        /// name, to a CSV or TSV file that each run starts anew; or saved as a picture of its own (PNG), named after the
        /// file: mask.png makes mask-in000001.png, mask-in000002.png, ... in its folder.
        /// </summary>
        [JsonProperty(Order = 4)]
        public bool PerFile
        {
            get => _perFile;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _perFile, value);
                Update();
            }
        }

        /// <summary>
        /// Rows go to CSV and TSV files, pictures to PNG ones.
        /// </summary>
        public bool CanSavePerFile => Format is ArrayFile.Csv or ArrayFile.Tsv or ArrayFile.Png;

        /// <summary>
        /// Whether it saves something per file rather than writing the file anew.
        /// </summary>
        public bool SavesPerFile => PerFile && CanSavePerFile;

        /// <summary>
        /// The per-file option, as the edit panel says it.
        /// </summary>
        public string PerFileLabel => Format == ArrayFile.Png ? "A picture per file" : "One row per file";

        /// <summary>
        /// Julia code to run before the first file: the file of rows started anew, or the folder of pictures made.
        /// </summary>
        public string StartPerFile => Format == ArrayFile.Png
            ? $"mkpath(dirname(abspath({ArrayFile.PathLiteral(_path)})))"
            : $"rm({ArrayFile.PathLiteral(_path)}; force = true)";

        /// <summary>
        /// Julia code saving <paramref name="input"/> for one file: a row, or a picture; <paramref name="named"/>: after,
        /// or named after, the file being gone through (<c>_path</c>).
        /// </summary>
        public string PerFileStatement(string input, bool named)
        {
            string path = ArrayFile.PathLiteral(_path);
            if (Format == ArrayFile.Png)
            {
                return named ? Picture(PerFilePath, input) : Picture(path, input);
            }

            // A text (a note, say) is one cell, not a cell a character.
            string cells = $"({input} isa AbstractString ? [{input}] : vec(collect({input})))";
            string row = named ? $"[basename(_path); {cells}]" : cells;
            string delimiter = Format == ArrayFile.Tsv ? "'\\t'" : "','";
            return $"open(io -> writedlm(io, permutedims({row}), {delimiter}), {path}, \"a\")";
        }

        /// <summary>
        /// <see cref="PerFileStatement"/> for a run: it reports where the row or the picture went.
        /// </summary>
        public string JuliaPerFile(string input, bool named) => Format == ArrayFile.Png && named
            ? $"let p = {PerFilePath}\n    {Picture("p", input)}\n    abspath(p)\nend"
            : $"begin\n    {PerFileStatement(input, named)}\n    abspath({ArrayFile.PathLiteral(_path)})\nend";

        /// <summary>
        /// The picture of the file being gone through: mask.png's folder, mask-in000001.png.
        /// </summary>
        private string PerFilePath => $"joinpath(dirname(abspath({ArrayFile.PathLiteral(_path)})), string(\"{System.IO.Path.GetFileNameWithoutExtension(_path)}-\", splitext(basename(_path))[1], \".png\"))";

        /// <summary>
        /// Julia code saving the matrix <paramref name="input"/> as a grey picture at <paramref name="path"/>: 0 black, 1 white.
        /// </summary>
        private static string Picture(string path, string input) => $"PNGFiles.save({path}, ColorTypes.Gray.(clamp.(Float64.({input}), 0, 1)))";

        public bool IsJld2 => Format == ArrayFile.Jld2;

        public string FileName => _path.Length == 0 ? "no file" : System.IO.Path.GetFileName(_path);

        public override string? Problem => _path.Length == 0 ? "Pick a file to save to."
            : IsJld2 && !IsIdentifier(NameInFile) ? "Give the value a name to store it under."
            : null;

        /// <summary>
        /// Shown on the node when there's a problem.
        /// </summary>
        public string? Error => Problem;

        public override IEnumerable<string> Packages => Format switch
        {
            ArrayFile.Csv or ArrayFile.Tsv => ["DelimitedFiles"],
            ArrayFile.MatrixMarket => ["MatrixMarket", "SparseArrays"],
            ArrayFile.Png => ["PNGFiles"],
            ArrayFile.Json => ["JSON"],
            _ => ["JLD2"],
        };

        /// <summary>
        /// The Julia code writing a value called <c>value</c>, for the edit panel.
        /// </summary>
        public string Code => $"using {string.Join(", ", Packages)}\n{(SavesPerFile ? PerFileStatement(Input.Name, named: true) : Statement(Input.Name))}";
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
                ArrayFile.Png => Picture(path, input),
                ArrayFile.Json => $"write({path}, JSON.json({input}))",
                _ => $"JLD2.jldsave({path}; {NameInFile} = {input})",
            };
        }

        private void Update()
        {
            // Matrix Market files hold sparse matrices; the others take anything writedlm or JLD2 can write.
            Input.Type = Format == ArrayFile.MatrixMarket ? JuliaTypes.SparseMatrix : Function.AnyType;

            foreach (string name in new[] { nameof(Format), nameof(IsJld2), nameof(CanSavePerFile), nameof(SavesPerFile), nameof(PerFileLabel), nameof(FileName), nameof(Problem), nameof(Error), nameof(Code) })
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
