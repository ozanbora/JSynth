using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// One choice of an example's data to download: a few files for a quick look, or all of them.
    /// </summary>
    /// <param name="Name">"Short" or "Full"; its lower case names its subfolder.</param>
    /// <param name="Description">What it holds, for the question before downloading.</param>
    /// <param name="Files">The names of its files.</param>
    /// <param name="ApproximateBytes">About how much there is to download.</param>
    public sealed record ExampleDataSet(string Name, string Description, IReadOnlyList<string> Files, long ApproximateBytes)
    {
        /// <summary>
        /// "5 MB".
        /// </summary>
        public string Size => ApproximateBytes >= 1_000_000 ? $"{ApproximateBytes / 1_000_000.0:0} MB" : $"{ApproximateBytes / 1000.0:0} kB";

        /// <summary>
        /// "Short (12 files, 5 MB)": the button choosing it.
        /// </summary>
        public string Label => $"{Name} ({Files.Count} files, {Size})";
    }

    /// <summary>
    /// Data an example reads that isn't bundled with JuliaSynth: files downloaded, when the example is
    /// opened and the user agrees, into a folder of the user's choosing, each set into a subfolder of it.
    /// </summary>
    /// <param name="Name">What the data is, e.g. "Boreas radar scans"; also names its folder.</param>
    /// <param name="About">Where it comes from and what it holds, for the question before downloading.</param>
    /// <param name="Credit">The attribution its licence asks for, written next to the files.</param>
    /// <param name="BaseUrl">Where the files are, each at <c>BaseUrl + name</c>.</param>
    /// <param name="Sets">The choices to download, the smallest first.</param>
    public sealed record ExampleData(string Name, string About, string Credit, string BaseUrl, IReadOnlyList<ExampleDataSet> Sets)
    {
        /// <summary>
        /// Where the files are when they come in an archive, rather than one by one at <see cref="BaseUrl"/>.
        /// </summary>
        public ExampleArchive? Archive { get; init; }

        /// <summary>
        /// Where each file is, by its name, when that isn't <see cref="BaseUrl"/> + name: a query of a web service, say.
        /// </summary>
        public Func<string, string>? FileUrl { get; init; }

        /// <summary>
        /// Writes a downloaded file (its name, its contents, where to) as it is to be kept, e.g. its pieces joined;
        /// written as downloaded unless given.
        /// </summary>
        public Action<string, Stream, Stream>? Convert { get; init; }

        /// <summary>
        /// Writes the GeoCSV file in <paramref name="from"/> (a seismometer's readings, as FDSN web services give them,
        /// maybe in pieces, each with its own lines describing it) to <paramref name="to"/> as one piece: the first
        /// piece's description, its header line, then every reading in order.
        /// </summary>
        public static void JoinGeoCsv(Stream from, Stream to)
        {
            using StreamReader reader = new(from);
            List<string> description = [];
            List<string> readings = [];
            bool described = false;
            while (reader.ReadLine() is string line)
            {
                bool comment = line.StartsWith('#');
                bool number = line.Length > 0 && (char.IsAsciiDigit(line[0]) || line[0] is '-' or '+' or '.');
                if (number)
                {
                    readings.Add(line);
                }
                else if (!described)
                {
                    description.Add(line);
                }

                // The first piece's header line ends its description; later pieces' are left out.
                described |= !comment && !number && line.Length > 0;
            }

            // The description counts the readings of them all.
            using StreamWriter writer = new(to, new System.Text.UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
            foreach (string line in description)
            {
                writer.WriteLine(line.StartsWith("# sample_count:", StringComparison.Ordinal) ? $"# sample_count: {readings.Count}" : line);
            }

            foreach (string line in readings)
            {
                writer.WriteLine(line);
            }
        }

        /// <summary>
        /// The file next to the data that says where it comes from.
        /// </summary>
        public const string ReadmeName = "README.txt";

        /// <summary>
        /// Where the data goes unless the user picks another folder: Documents/JuliaSynth/Example data/Name.
        /// </summary>
        public string DefaultFolder => FolderOf(Name);

        /// <summary>
        /// Documents/JuliaSynth/Example data/<paramref name="name"/>: where examples keep their data.
        /// </summary>
        public static string FolderOf(string name)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents.Length > 0 ? documents : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "JuliaSynth", "Example data", name);
        }

        /// <summary>
        /// Where the data is (or would go): the folder it was downloaded to before, else the default one.
        /// </summary>
        public string Folder => AppSettings.Load().DataFolders.TryGetValue(Name, out string? folder) ? folder : DefaultFolder;

        /// <summary>
        /// Where <paramref name="set"/> goes in <paramref name="folder"/>: a subfolder of its own, so a
        /// folder of files read as a whole holds one set only.
        /// </summary>
        public static string SetFolder(string folder, ExampleDataSet set) => Path.Combine(folder, set.Name.ToLowerInvariant());

        /// <summary>
        /// Whether every file of <paramref name="set"/> is in <paramref name="folder"/>.
        /// </summary>
        public static bool IsIn(string folder, ExampleDataSet set)
        {
            string files = SetFolder(folder, set);
            return set.Files.All(f => new FileInfo(Path.Combine(files, f)) is { Exists: true, Length: > 0 });
        }

        /// <summary>
        /// The folder of the largest set downloaded in full, or null when none is.
        /// </summary>
        public string? DownloadedFolder
        {
            get
            {
                string folder = Folder;
                return Sets.Reverse().Where(s => IsIn(folder, s)).Select(s => SetFolder(folder, s)).FirstOrDefault();
            }
        }

        /// <summary>
        /// Downloads the files of <paramref name="set"/> missing from its subfolder of <paramref name="folder"/>
        /// one by one (each written to a temporary name first, so a stopped download leaves no half file),
        /// with the credit next to them, and remembers the folder. <paramref name="progress"/> is told the
        /// number of files there so far, right away on the caller's thread (the download goes on there
        /// between its awaits), so it never comes after the end.
        /// </summary>
        /// <exception cref="HttpRequestException">A file couldn't be downloaded.</exception>
        /// <exception cref="IOException">A file couldn't be written.</exception>
        /// <exception cref="OperationCanceledException">Stopped.</exception>
        public async Task DownloadAsync(HttpClient client, string folder, ExampleDataSet set, Action<int>? progress = null, CancellationToken cancellationToken = default)
        {
            string files = SetFolder(folder, set);
            _ = Directory.CreateDirectory(files);
            await File.WriteAllTextAsync(Path.Combine(folder, ReadmeName), $"{Name}\n\n{About}\n\n{Credit}\n\nDownloaded by JuliaSynth from {BaseUrl}\n", cancellationToken);

            int done = 0;
            if (Archive is ExampleArchive archive)
            {
                if (!IsIn(folder, set))
                {
                    await archive.ExtractAsync(client, set.Files, files, () => progress?.Invoke(++done), cancellationToken);
                }

                Remember(folder);
                return;
            }

            foreach (string name in set.Files)
            {
                string path = Path.Combine(files, name);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (!new FileInfo(path).Exists)
                {
                    string partial = path + ".part";
                    using (HttpResponseMessage response = await client.GetAsync(FileUrl is null ? BaseUrl + name : FileUrl(name), HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        _ = response.EnsureSuccessStatusCode();
                        await using FileStream file = File.Create(partial);
                        if (Convert is null)
                        {
                            await response.Content.CopyToAsync(file, cancellationToken);
                        }
                        else
                        {
                            using MemoryStream contents = new();
                            await response.Content.CopyToAsync(contents, cancellationToken);
                            contents.Position = 0;
                            Convert(name, contents, file);
                        }
                    }

                    File.Move(partial, path, overwrite: true);
                }

                progress?.Invoke(++done);
            }

            Remember(folder);
        }

        private void Remember(string folder)
        {
            AppSettings settings = AppSettings.Load();
            settings.DataFolders[Name] = folder;
            settings.Save();
        }
    }
}
