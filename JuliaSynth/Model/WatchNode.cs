using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A folder watched for new files, a sensor's say: a design with one runs as a stream, the nodes
    /// downstream of it again for every file as soon as it is written, until it is stopped. It gives
    /// each file's contents (a frame) in turn; an input node with a next value wired (see
    /// <see cref="Node.Next"/>) carries a state from file to file. Its settings for reading the files are
    /// those of <see cref="FilesNode"/>.
    /// </summary>
    public class WatchNode : FilesNode
    {
        public const string ByName = "Name";
        public const string ByTime = "Time written";

        public static IReadOnlyList<string> Orders { get; } = [ByName, ByTime];

        /// <summary>
        /// Julia code watching a folder, for runs and exported scripts alike: <c>_jsynth_watch</c> calls
        /// back for each file once it is written.
        /// </summary>
        public static new string Prelude { get; } = """
            # A folder watched for new files, made by a JuliaSynth Watch folder node: body(path, n, show) for
            # each file once it is written (its size the same at two looks), in name or time order. Files there
            # before the start go first, or are passed over. show: whether to show values for this one, at most
            # four times a second and for the last of those waiting. ready() once it watches. Ends when the stop file appears.
            # latest: when several files are waiting, only the newest is gone through.
            # also: folders in step; a file waits until each has a file of its name, written too.
            # more: folders merged with this one, (folder, pattern) each: their files are gone through too, as they come.
            function _jsynth_watch(body, folder, pattern, existing, bytime; stop = nothing, ready = () -> nothing, latest = false, also = String[], more = [])
                watched = [(folder, pattern); more]
                for (f, _) in watched
                    isdir(f) || throw(ArgumentError("The folder $(repr(f)) doesn't exist."))
                end
                # The files to go through, with the number of the folder each is in.
                function files()
                    found = Tuple{String, Int}[]
                    for (k, (f, r)) in enumerate(watched), p in readdir(f; join = true)
                        isfile(p) && occursin(r, basename(p)) && push!(found, (p, k))
                    end
                    found
                end
                stopped() = stop !== nothing && isfile(stop)
                seen = existing ? Set{String}() : Set(first.(files()))
                ready()
                sizes = Dict{String, Vector{Int}}()
                # A file and its twins in the folders in step, each written: its size and theirs the same at two looks.
                looks(p) = [filesize(p); [filesize(joinpath(f, basename(p))) for f in also]]
                n, shown = 0, 0.0
                while !stopped()
                    due = Tuple{String, Int}[]
                    for (p, k) in files()
                        p in seen && continue
                        s = looks(p)
                        all(>(0), s) && get(sizes, p, Int[]) == s ? push!(due, (p, k)) : (sizes[p] = s)
                    end
                    # By name (the same name: by folder), or by the time written.
                    sort!(due; by = ((p, k),) -> (bytime ? mtime(p) : 0.0, basename(p), k))
                    due = first.(due)
                    # Latest only: the files waiting before the newest are passed over.
                    if latest && length(due) > 1
                        foreach(p -> (push!(seen, p); delete!(sizes, p)), due[1:end - 1])
                        due = due[end:end]
                    end
                    for (i, p) in enumerate(due)
                        stopped() && return n
                        push!(seen, p)
                        delete!(sizes, p)
                        n += 1
                        show = i == length(due) || time() - shown >= 0.25
                        show && (shown = time())
                        body(p, n, show)
                    end
                    isempty(due) && sleep(0.1)
                end
                n
            end
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// What a run reports for the watched folder once it watches, before any file: so a replay starts then.
        /// </summary>
        public const string WatchingText = "Watching for files…";

        /// <summary>
        /// Julia code for a run going through files one at a time, watched or in a folder: each file starts
        /// with the failures there were before the loop (one file failing doesn't stop the next), values are
        /// shown at most four times a second, and the node giving the files says how far it got.
        /// </summary>
        public static string RunPrelude { get; } = $$"""
            const _jsynth_total = Ref(0)
            function _jsynth_frame_start(show, before, held)
                _jsynth_quiet[] = !show
                empty!(_jsynth_failed)
                union!(_jsynth_failed, before)
                empty!(_jsynth_held)
                union!(_jsynth_held, held)
            end
            _jsynth_watching(id) = _jsynth_report(id, "ok", "", "{{WatchingText}}")
            _jsynth_frame(id, n, path, value) = _jsynth_report(id, "ok", string(typeof(value)),
                _jsynth_total[] > 0 ? string("file ", n, " of ", _jsynth_total[], ": ", basename(path))
                : string(n, n == 1 ? " file" : " files", " so far; the latest: ", basename(path)))
            # A tick of a clock, or a press of a button: its number, and the seconds since the start.
            _jsynth_tick(id, n, t, what) = _jsynth_report(id, "ok", "Int64", string(what, " ", n, ", ", round(t; digits = 1), " s in"))
            # The files of a Files node going one at a time: body(path, n, show) for each, in name order.
            function _jsynth_each(body, id, files)
                paths = try
                    files().paths
                catch e
                    _jsynth_error(id, e)
                    return 0
                end
                _jsynth_total[] = length(paths)
                isempty(paths) && _jsynth_report(id, "ok", "", "No file to go through.")
                shown = 0.0
                for (n, p) in enumerate(paths)
                    show = n == length(paths) || time() - shown >= 0.25
                    show && (shown = time())
                    body(p, n, show)
                end
                length(paths)
            end
            """.ReplaceLineEndings("\n");

        private bool _existingFirst = true;
        private string _order = ByName;
        private string _replayFolder = string.Empty;
        private double _replayRate = 4;
        private bool _latestOnly;
        private string _fetchUrl = string.Empty;
        private double _fetchEvery = 15;

        /// <summary>
        /// Whether files already in the folder when the stream starts go first, or are passed over.
        /// </summary>
        [JsonProperty(Order = 8)]
        public bool ExistingFirst
        {
            get => _existingFirst;
            set => this.RaiseAndSetIfChanged(ref _existingFirst, value);
        }

        /// <summary>
        /// The order of files arriving together: by <see cref="ByName"/> (timestamped names), or <see cref="ByTime"/> written.
        /// </summary>
        [JsonProperty(Order = 9)]
        public string Order
        {
            get => _order;
            set
            {
                if (value is not null && Orders.Contains(value))
                {
                    _ = this.RaiseAndSetIfChanged(ref _order, value);
                }
            }
        }

        /// <summary>
        /// Recorded files JuliaSynth copies into the watched folder while the stream runs, one at a time at
        /// <see cref="ReplayRate"/>: a stand-in for a sensor. Empty for none.
        /// </summary>
        [JsonProperty(Order = 10)]
        public string ReplayFolder
        {
            get => _replayFolder;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _replayFolder, (value ?? string.Empty).Trim().Trim('"'));
                this.RaisePropertyChanged(nameof(Replays));
            }
        }

        /// <summary>
        /// Files a second the replay writes, from 0.1 to 100.
        /// </summary>
        [JsonProperty(Order = 11)]
        public double ReplayRate
        {
            get => _replayRate;
            set => this.RaiseAndSetIfChanged(ref _replayRate, Math.Clamp(double.IsFinite(value) ? value : 4, 0.1, 100));
        }

        public bool Replays => _replayFolder.Length > 0;

        /// <summary>
        /// When several files are waiting, only the newest is worked through, the others passed over: for live data,
        /// where only the present matters.
        /// </summary>
        [JsonProperty(Order = 13)]
        public bool LatestOnly
        {
            get => _latestOnly;
            set => this.RaiseAndSetIfChanged(ref _latestOnly, value);
        }

        /// <summary>
        /// A web address JuliaSynth fetches again and again while the stream runs, saving each answer as a new file in
        /// the watched folder, named by the time: a web service as the sensor. Empty for none.
        /// </summary>
        [JsonProperty(Order = 14)]
        public string FetchUrl
        {
            get => _fetchUrl;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _fetchUrl, (value ?? string.Empty).Trim());
                this.RaisePropertyChanged(nameof(Fetches));
                Update();
            }
        }

        /// <summary>
        /// Seconds between fetches, from 1 to a day.
        /// </summary>
        [JsonProperty(Order = 15)]
        public double FetchEvery
        {
            get => _fetchEvery;
            set => this.RaiseAndSetIfChanged(ref _fetchEvery, Math.Clamp(double.IsFinite(value) ? value : 15, 1, 86_400));
        }

        public bool Fetches => _fetchUrl.Length > 0;

        protected override bool MakesFolder => Fetches;

        /// <summary>
        /// The name of a fetched answer's file: the time it was fetched (UTC), with the extension of the files watched
        /// for, as they sort in time order: 2026-10-05T07-12-30.json.
        /// </summary>
        public string FetchedName(DateTime utc)
        {
            string extension = System.IO.Path.GetExtension(Pattern) is { Length: > 1 } ext && !ext.Contains('*') && !ext.Contains('?') ? ext : ".txt";
            return utc.ToString("yyyy-MM-dd'T'HH-mm-ss", CultureInfo.InvariantCulture) + extension;
        }

        /// <summary>
        /// Always one file at a time, as they come.
        /// </summary>
        [JsonIgnore]
        public override bool EachFile
        {
            get => true;
            set { }
        }

        public override string OutputType => EachType;

        protected override bool NeedsFiles => false;

        // A stream goes on from the folder it started with: not one to set from the panel.
        public override bool CanBeInPanel => false;

        public WatchNode()
        {
            Color = NodePalette.Watch;
        }

        /// <summary>
        /// The call watching the folder, <paramref name="body"/> being <c>(path, n, show)</c> handling one file.
        /// </summary>
        /// <param name="stopFile">For a run: the file whose appearing ends the stream; the run is told when it watches.</param>
        /// <param name="inStep">The folders in step with this one (a Zip node's others): a file waits until each has its twin.</param>
        /// <param name="merged">The folders merged with this one (a Merge node's others): their files are gone through too.</param>
        public string Watch(string? stopFile, IReadOnlyList<string>? inStep = null, IReadOnlyList<FilesNode>? merged = null)
        {
            List<string> options = [];
            if (LatestOnly)
            {
                options.Add("latest = true");
            }

            if (inStep is { Count: > 0 })
            {
                options.Add($"also = {FoldersJulia(inStep)}");
            }

            if (merged is { Count: > 0 })
            {
                options.Add($"more = [{string.Join(", ", merged.Select(m => $"({ArrayFile.PathLiteral(m.Folder)}, r\"{PatternRegex(m.Pattern)}\"i)"))}]");
            }

            if (stopFile is not null)
            {
                options.Add($"stop = {ArrayFile.PathLiteral(stopFile)}, ready = () -> _jsynth_watching(\"{ID}\")");
            }

            string named = options.Count == 0 ? string.Empty : $"; {string.Join(", ", options)}";
            return $"_jsynth_watch({ArrayFile.PathLiteral(Folder)}, r\"{PatternRegex(Pattern)}\"i, {(ExistingFirst ? "true" : "false")}, {(Order == ByTime ? "true" : "false")}{named})";
        }

        public override string Julia => $"{Watch(null)} do path, n, show\n    frame = ({ReadFunction})(path)\n    # ... the nodes downstream, for each file\nend";

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property is nameof(Replays) or nameof(Fetches);
        }

        /// <summary>
        /// "4 files a second".
        /// </summary>
        public string ReplayRateText => $"{ReplayRate.ToString("0.##", CultureInfo.InvariantCulture)} files a second";
    }
}
