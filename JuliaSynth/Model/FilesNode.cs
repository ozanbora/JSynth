using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace JuliaSynth.Model
{
    /// <summary>
    /// The data files of a folder whose names match a pattern, read one at a time when something goes
    /// through them (a <see cref="MapNode"/>), so a large folder never has to fit in memory.
    /// </summary>
    public class FilesNode : Node
    {
        /// <summary>
        /// Julia code defining the value of a Files node: the files' paths and how to read one, iterable
        /// (reading each file as it is reached) and shown as the files it holds.
        /// </summary>
        public static string Prelude { get; } = """
            # Files read one at a time, as they are iterated over; made by a JuliaSynth Files node.
            struct JuliaSynthFiles
                folder::String
                paths::Vector{String}
                read::Function
            end
            function JuliaSynthFiles(folder::AbstractString, pattern::Regex, read::Function)
                isdir(folder) || throw(ArgumentError("The folder $(repr(folder)) doesn't exist."))
                paths = filter(p -> isfile(p) && occursin(pattern, basename(p)), readdir(folder; join = true))
                JuliaSynthFiles(folder, paths, read)
            end
            Base.iterate(files::JuliaSynthFiles, i = 1) = i > length(files.paths) ? nothing : (files.read(files.paths[i]), i + 1)
            Base.length(files::JuliaSynthFiles) = length(files.paths)
            Base.IteratorEltype(::Type{JuliaSynthFiles}) = Base.EltypeUnknown()
            function Base.show(io::IO, files::JuliaSynthFiles)
                names = basename.(files.paths)
                print(io, length(names), length(names) == 1 ? " file" : " files", " in ", files.folder)
                isempty(names) || print(io, ": ", join(first(names, 5), ", "), length(names) > 5 ? ", …" : "")
            end
            """.ReplaceLineEndings("\n");

        #region Private Properties
        private string _folder = string.Empty;
        private string _format = FileFormats.Csv;
        private string _pattern = FileFormats.Pattern(FileFormats.Csv);
        private string _shape = JuliaTypes.Matrix;
        private string _elementType = "Float64";
        private bool _hasHeader;
        private string _variable = "A";
        private string _summary = string.Empty;
        private string? _error;
        private bool _eachFile;
        #endregion

        #region Public Properties
        /// <summary>
        /// The folder holding the files, as Julia will be given it.
        /// </summary>
        // Saved and read back in order: the format sets the pattern, unless one was typed.
        [JsonProperty(Order = 1)]
        public string Folder
        {
            get => _folder;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _folder, (value ?? string.Empty).Trim().Trim('"'));
                Update();
            }
        }

        [JsonProperty(Order = 2)]
        public string Format
        {
            get => _format;
            set
            {
                if (value is null || !FileFormats.All.Contains(value) || value == _format)
                {
                    return;
                }

                // The pattern goes with the format, unless it was changed by hand.
                if (_pattern == FileFormats.Pattern(_format))
                {
                    _pattern = FileFormats.Pattern(value);
                    this.RaisePropertyChanged(nameof(Pattern));
                }

                _ = this.RaiseAndSetIfChanged(ref _format, value);
                Update();
            }
        }

        /// <summary>
        /// Which files to read, by name: * stands for any text and ? for one character, e.g. <c>run-*.csv</c>.
        /// </summary>
        [JsonProperty(Order = 3)]
        public string Pattern
        {
            get => _pattern;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _pattern, (value ?? string.Empty).Trim());
                Update();
            }
        }

        /// <summary>
        /// What each file is read as: Vector, Matrix, SparseMatrixCSC, ...; which ones depends on the format.
        /// </summary>
        [JsonProperty(Order = 4)]
        public string Shape
        {
            get => _shape;
            set
            {
                if (value is not null && JuliaTypes.Shapes.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _shape, value);
                    Update();
                }
            }
        }

        [JsonProperty(Order = 5)]
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
        /// Whether the first line of each CSV or TSV file holds column names, to be skipped.
        /// </summary>
        [JsonProperty(Order = 6)]
        public bool HasHeader
        {
            get => _hasHeader;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _hasHeader, value);
                Update();
            }
        }

        /// <summary>
        /// The name the array was saved under in each JLD2 file.
        /// </summary>
        [JsonProperty(Order = 7)]
        public string Variable
        {
            get => _variable;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _variable, (value ?? string.Empty).Trim());
                Update();
            }
        }

        /// <summary>
        /// Whether the files are gone through one at a time: the node gives each file's contents in turn, and
        /// the nodes after it are worked out again for each, with inputs taking their next values in between
        /// (see <see cref="Node.Next"/>). Otherwise it gives all the files, for a Map to go through.
        /// </summary>
        [JsonProperty(Order = 8)]
        public virtual bool EachFile
        {
            get => _eachFile;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _eachFile, value);
                this.RaisePropertyChanged(nameof(OutputType));
                this.RaisePropertyChanged(nameof(EachText));
            }
        }

        public override string OutputType => EachFile ? EachType : Function.AnyType;

        /// <summary>
        /// On the node: what it gives, "each a Matrix{Float64}", one at a time or all together.
        /// </summary>
        public string EachText => EachFile ? $"one at a time, each a {EachType}" : $"each a {EachType}";

        public IReadOnlyList<string> PossibleShapes => FileFormats.ShapesFor(Format);

        public bool CanPickShape => PossibleShapes.Count > 1;

        public bool IsDelimited => FileFormats.IsDelimited(Format);

        public bool IsJld2 => Format == FileFormats.Jld2;

        public bool IsImage => FileFormats.IsImage(Format);

        /// <summary>
        /// What each file becomes, e.g. <c>Matrix{Float64}</c>: the type a function applied to them takes.
        /// </summary>
        public string EachType => JuliaTypes.Compose(Shape, ElementType);

        public string FolderName => _folder.Length == 0 ? "no folder" : System.IO.Path.GetFileName(_folder.TrimEnd('/', '\\')) is { Length: > 0 } name ? name : _folder;

        /// <summary>
        /// How many files match, and the first few, when that can be told from here.
        /// </summary>
        public string Summary
        {
            get => _summary;
            private set => this.RaiseAndSetIfChanged(ref _summary, value);
        }

        /// <summary>
        /// The Julia expression making the files, e.g. <c>JuliaSynthFiles(raw"data", r"^.*\.csv$"i, path -> readdlm(path, ',', Float64))</c>.
        /// </summary>
        public virtual string Julia => $"JuliaSynthFiles({ArrayFile.PathLiteral(_folder)}, r\"{PatternRegex(_pattern)}\"i, {ReadFunction})";

        /// <summary>
        /// The Julia function reading one file, e.g. <c>path -> readdlm(path, ',', Float64)</c>.
        /// </summary>
        public string ReadFunction => $"path -> {FileFormats.Read(Format, "path", Shape, ElementType, HasHeader, Variable)}";

        /// <summary>
        /// <see cref="Julia"/>, after the <c>using</c> it needs.
        /// </summary>
        public string Code => $"using {Package}\n{Julia}";

        public string Package => FileFormats.Package(Format);

        public string? Error
        {
            get => _error;
            private set => this.RaiseAndSetIfChanged(ref _error, value);
        }
        #endregion

        public FilesNode()
        {
            Color = NodePalette.Files;
            Update();
        }

        /// <summary>
        /// The regular expression a file name pattern stands for (case is ignored where it is used):
        /// <c>*.csv</c> → <c>^.*\.csv$</c>.
        /// </summary>
        public static string PatternRegex(string pattern)
        {
            return "^" + string.Concat((pattern.Length == 0 ? "*" : pattern).Select(c => c switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(c.ToString()),
            })) + "$";
        }

        /// <summary>
        /// Looks at the folder again, for <see cref="Summary"/> and <see cref="Error"/>: files come and go.
        /// </summary>
        public void Recount()
        {
            (Summary, Error) = Inspect();
        }

        /// <summary>
        /// The summary and error are worked out from the folder: looking again changes no design.
        /// </summary>
        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property is nameof(Summary) or nameof(Error);
        }

        /// <summary>
        /// Whether a folder without matching files is an error: it is for going through them, not for
        /// waiting for them.
        /// </summary>
        protected virtual bool NeedsFiles => true;

        /// <summary>
        /// Checks the settings again and says what changed.
        /// </summary>
        protected void Update()
        {
            if (!PossibleShapes.Contains(_shape))
            {
                _shape = PossibleShapes.Contains(JuliaTypes.Matrix) ? JuliaTypes.Matrix : PossibleShapes[0];
            }

            (Summary, Error) = Inspect();

            foreach (string name in new[]
            {
                nameof(Shape), nameof(PossibleShapes), nameof(CanPickShape), nameof(IsDelimited), nameof(IsJld2), nameof(IsImage),
                nameof(EachType), nameof(EachText), nameof(OutputType), nameof(FolderName), nameof(Julia), nameof(Code), nameof(Package),
            })
            {
                this.RaisePropertyChanged(name);
            }
        }

        private (string Summary, string? Error) Inspect()
        {
            if (_folder.Length == 0)
            {
                return (string.Empty, "Pick a folder.");
            }

            if (_pattern.IndexOfAny(['"', '/', '\\']) >= 0)
            {
                return (string.Empty, "The pattern is for file names: no quotes or folders in it.");
            }

            if (IsJld2 && !IsIdentifier(Variable))
            {
                return (string.Empty, "Name the array as it was saved in the files.");
            }

            // A relative folder is found from wherever Julia runs, so it can't be checked here.
            if (!System.IO.Path.IsPathFullyQualified(_folder))
            {
                return (string.Empty, null);
            }

            if (!Directory.Exists(_folder))
            {
                return (string.Empty, "The folder doesn't exist.");
            }

            try
            {
                Regex matches = new(PatternRegex(_pattern), RegexOptions.IgnoreCase);
                List<string> names = Directory.EnumerateFiles(_folder)
                    .Select(f => System.IO.Path.GetFileName(f))
                    .Where(n => matches.IsMatch(n))
                    .Order(StringComparer.Ordinal)
                    .ToList();

                string pattern = _pattern.Length == 0 ? "*" : _pattern;
                return names.Count == 0
                    ? (NeedsFiles ? (string.Empty, $"No file in the folder matches {pattern}.") : ($"No {pattern} file yet: waiting for the first.", null))
                    : ($"{names.Count:N0} {(names.Count == 1 ? "file" : "files")}: {string.Join(", ", names.Take(3))}{(names.Count > 3 ? ", …" : string.Empty)}", null);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return (string.Empty, $"The folder can't be read: {e.Message}");
            }
        }

        private static bool IsIdentifier(string name)
        {
            return name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '!');
        }
    }
}
