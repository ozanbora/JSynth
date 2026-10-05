using SharpCompress.Readers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// Example data that comes as an archive (zip, RAR, 7z, tar, ...), unpacked as it is downloaded, keeping the files
    /// wanted: the archive at <paramref name="Url"/>, or, when <paramref name="Entry"/> is given, that archive inside a
    /// large zip there on a server answering range requests, of which only that entry is downloaded. NASA's IMS
    /// bearing data, say: a 1 GB zip of three RAR archives, of which one is 86 MB.
    /// </summary>
    /// <param name="Url">The archive, or the zip holding it.</param>
    /// <param name="Entry">The archive's name in the zip, e.g. <c>IMS/2nd_test.rar</c>; null when the archive is at <paramref name="Url"/>.</param>
    public sealed record ExampleArchive(string Url, string? Entry)
    {
        // The end of a zip holding its list of entries: the central directory is in its last bytes.
        private const int TailBytes = 65_536;

        /// <summary>
        /// The name in the set of a file of the archive, by its path in it; null for a file not wanted. Its name,
        /// whatever folder it is in, unless given.
        /// </summary>
        public Func<string, string?>? Rename { get; init; }

        /// <summary>
        /// Writes a file of the archive (its path in it, its contents, where to) as it is to be kept, e.g. as a grey
        /// PNG; written unchanged unless given.
        /// </summary>
        public Action<string, Stream, Stream>? Convert { get; init; }

        /// <summary>
        /// Downloads the archive and writes the files of it named in <paramref name="wanted"/> (see <see cref="Rename"/>)
        /// to <paramref name="folder"/>, each to a temporary name first, so a stopped download leaves no half file.
        /// <paramref name="written"/> is called for each wanted file there, written or not.
        /// </summary>
        /// <exception cref="HttpRequestException">The archive couldn't be downloaded.</exception>
        /// <exception cref="InvalidDataException">The archive, or the zip holding it, isn't as expected.</exception>
        /// <exception cref="IOException">A file couldn't be written.</exception>
        public async Task ExtractAsync(HttpClient client, IReadOnlyCollection<string> wanted, string folder, Action written, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, Url);
            long offset = 0;
            if (Entry is not null)
            {
                (offset, long packed, int method) = await LocateAsync(client, cancellationToken);
                if (method != 8)
                {
                    throw new InvalidDataException($"{Entry} is stored in the zip in a way JuliaSynth can't read (method {method}).");
                }

                request.Headers.Range = new RangeHeaderValue(offset, offset + 30 + 65_535 + 65_535 + packed);
            }

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using Stream archive = Entry is null ? body : await DeflatedEntryAsync(body, cancellationToken);

            HashSet<string> left = [.. wanted];
            await using IAsyncReader reader = await ReaderFactory.OpenAsyncReader(archive, cancellationToken: cancellationToken);
            while (left.Count > 0 && await reader.MoveToNextEntryAsync(cancellationToken))
            {
                string key = (reader.Entry.Key ?? string.Empty).Replace('\\', '/');
                string? name = reader.Entry.IsDirectory ? null : Rename is null ? Path.GetFileName(key) : Rename(key);
                if (name is null || !left.Remove(name))
                {
                    continue;
                }

                string path = Path.Combine(folder, name);
                if (new FileInfo(path) is not { Exists: true, Length: > 0 })
                {
                    string partial = path + ".part";
                    await using (FileStream file = File.Create(partial))
                    {
                        if (Convert is null)
                        {
                            await reader.WriteEntryToAsync(file, cancellationToken);
                        }
                        else
                        {
                            using MemoryStream contents = new();
                            await reader.WriteEntryToAsync(contents, cancellationToken);
                            contents.Position = 0;
                            Convert(key, contents, file);
                        }
                    }

                    File.Move(partial, path, overwrite: true);
                }

                written();
            }

            if (left.Count > 0)
            {
                throw new InvalidDataException($"{Entry ?? Url} has no {left.Order(StringComparer.Ordinal).First()}{(left.Count > 1 ? $" (and {left.Count - 1} more)" : string.Empty)}.");
            }
        }

        /// <summary>
        /// Writes the picture in <paramref name="from"/> (JPEG, PNG, BMP, ...) to <paramref name="to"/> as an 8-bit grey
        /// PNG, the grey Julia's ColorTypes makes of its colours: 0.299 red + 0.587 green + 0.114 blue.
        /// </summary>
        /// <exception cref="InvalidDataException">It isn't a picture.</exception>
        public static void GreyPng(Stream from, Stream to)
        {
            using SKBitmap colour = SKBitmap.Decode(from) ?? throw new InvalidDataException("This isn't a picture JuliaSynth can read.");
            using SKBitmap grey = new(new SKImageInfo(colour.Width, colour.Height, SKColorType.Gray8, SKAlphaType.Opaque));
            byte[] levels = new byte[colour.Width * colour.Height];
            for (int y = 0; y < colour.Height; y++)
            {
                for (int x = 0; x < colour.Width; x++)
                {
                    SKColor c = colour.GetPixel(x, y);
                    levels[(y * colour.Width) + x] = (byte)Math.Round((0.299 * c.Red) + (0.587 * c.Green) + (0.114 * c.Blue));
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(levels, 0, grey.GetPixels(), levels.Length);
            using SKData png = grey.Encode(SKEncodedImageFormat.Png, 100);
            png.SaveTo(to);
        }

        /// <summary>
        /// The archive in a zip entry's bytes: its local header (30 bytes, its name and an extra field) skipped, the
        /// rest inflated.
        /// </summary>
        private async Task<Stream> DeflatedEntryAsync(Stream body, CancellationToken cancellationToken)
        {
            byte[] header = new byte[30];
            await body.ReadExactlyAsync(header, cancellationToken);
            if (BitConverter.ToUInt32(header, 0) != 0x04034b50)
            {
                throw new InvalidDataException($"The zip has no {Entry} where its list of files says.");
            }

            byte[] skipped = new byte[BitConverter.ToUInt16(header, 26) + BitConverter.ToUInt16(header, 28)];
            await body.ReadExactlyAsync(skipped, cancellationToken);
            return new DeflateStream(body, CompressionMode.Decompress, leaveOpen: true);
        }

        /// <summary>
        /// Where <see cref="Entry"/> starts in the zip, how long it is packed, and how: from the zip's list of
        /// entries at its end.
        /// </summary>
        private async Task<(long Offset, long Packed, int Method)> LocateAsync(HttpClient client, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, Url);
            request.Headers.Range = new RangeHeaderValue(null, TailBytes);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            byte[] tail = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            for (int i = 0; i + 46 <= tail.Length; i++)
            {
                if (BitConverter.ToUInt32(tail, i) != 0x02014b50)
                {
                    continue;
                }

                int nameLength = BitConverter.ToUInt16(tail, i + 28);
                if (i + 46 + nameLength <= tail.Length && System.Text.Encoding.UTF8.GetString(tail, i + 46, nameLength) == Entry)
                {
                    return (BitConverter.ToUInt32(tail, i + 42), BitConverter.ToUInt32(tail, i + 20), BitConverter.ToUInt16(tail, i + 10));
                }
            }

            throw new InvalidDataException($"The zip at {Url} has no {Entry}.");
        }
    }
}
