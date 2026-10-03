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
    /// Data an example reads that isn't bundled with JuliaSynth: files downloaded, when the example is
    /// opened and the user agrees, into a folder of the user's choosing.
    /// </summary>
    /// <param name="Name">What the data is, e.g. "Boreas radar scans"; also names its folder.</param>
    /// <param name="About">Where it comes from and what it holds, for the question before downloading.</param>
    /// <param name="Credit">The attribution its licence asks for, written next to the files.</param>
    /// <param name="BaseUrl">Where the files are, each at <c>BaseUrl + name</c>.</param>
    /// <param name="Files">The names of the files.</param>
    /// <param name="ApproximateBytes">About how much there is to download.</param>
    public sealed record ExampleData(string Name, string About, string Credit, string BaseUrl, IReadOnlyList<string> Files, long ApproximateBytes)
    {
        /// <summary>
        /// The file next to the data that says where it comes from.
        /// </summary>
        public const string ReadmeName = "README.txt";

        /// <summary>
        /// Where the data goes unless the user picks another folder: Documents/JuliaSynth/Example data/Name.
        /// </summary>
        public string DefaultFolder
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents.Length > 0 ? documents : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "JuliaSynth", "Example data", Name);
            }
        }

        /// <summary>
        /// Where the data is (or would go): the folder it was downloaded to before, else the default one.
        /// </summary>
        public string Folder => AppSettings.Load().DataFolders.TryGetValue(Name, out string? folder) ? folder : DefaultFolder;

        /// <summary>
        /// Whether every file is in <paramref name="folder"/>.
        /// </summary>
        public bool IsIn(string folder)
        {
            return Files.All(f => new FileInfo(Path.Combine(folder, f)) is { Exists: true, Length: > 0 });
        }

        /// <summary>
        /// "5 MB", for the question before downloading.
        /// </summary>
        public string Size => ApproximateBytes >= 1_000_000 ? $"{ApproximateBytes / 1_000_000.0:0} MB" : $"{ApproximateBytes / 1000.0:0} kB";

        /// <summary>
        /// Downloads the files missing from <paramref name="folder"/> one by one (each written to a temporary
        /// name first, so a stopped download leaves no half file), with the credit next to them, and
        /// remembers the folder. <paramref name="progress"/> gets the number of files there so far.
        /// </summary>
        /// <exception cref="HttpRequestException">A file couldn't be downloaded.</exception>
        /// <exception cref="IOException">A file couldn't be written.</exception>
        /// <exception cref="OperationCanceledException">Stopped.</exception>
        public async Task DownloadAsync(HttpClient client, string folder, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            _ = Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, ReadmeName), $"{Name}\n\n{About}\n\n{Credit}\n\nDownloaded by JuliaSynth from {BaseUrl}\n", cancellationToken);

            int done = 0;
            foreach (string name in Files)
            {
                string path = Path.Combine(folder, name);
                if (!new FileInfo(path).Exists)
                {
                    string partial = path + ".part";
                    using (HttpResponseMessage response = await client.GetAsync(BaseUrl + name, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        _ = response.EnsureSuccessStatusCode();
                        await using FileStream file = File.Create(partial);
                        await response.Content.CopyToAsync(file, cancellationToken);
                    }

                    File.Move(partial, path, overwrite: true);
                }

                progress?.Report(++done);
            }

            AppSettings settings = AppSettings.Load();
            settings.DataFolders[Name] = folder;
            settings.Save();
        }
    }
}
