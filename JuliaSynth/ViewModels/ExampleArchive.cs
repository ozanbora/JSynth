using SharpCompress.Readers;
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
    /// Example data that comes as an archive (RAR, 7z, tar, ...) inside a large zip on a server answering
    /// range requests: only that entry of the zip is downloaded, and it is unpacked as it comes, keeping the
    /// files wanted. NASA's IMS bearing data, say: a 1 GB zip of three RAR archives, of which one is 86 MB.
    /// </summary>
    /// <param name="ZipUrl">The zip.</param>
    /// <param name="Entry">The archive's name in the zip, e.g. <c>IMS/2nd_test.rar</c>.</param>
    public sealed record ExampleArchive(string ZipUrl, string Entry)
    {
        // The end of a zip holding its list of entries: the central directory is in its last bytes.
        private const int TailBytes = 65_536;

        /// <summary>
        /// Downloads the archive and writes the files of it named in <paramref name="wanted"/> (by name, whatever
        /// folder they are in) to <paramref name="folder"/>, each to a temporary name first, so a stopped download
        /// leaves no half file. <paramref name="written"/> is called for each wanted file there, written or not.
        /// </summary>
        /// <exception cref="HttpRequestException">The archive couldn't be downloaded.</exception>
        /// <exception cref="InvalidDataException">The zip, or the archive in it, isn't as expected.</exception>
        /// <exception cref="IOException">A file couldn't be written.</exception>
        public async Task ExtractAsync(HttpClient client, IReadOnlyCollection<string> wanted, string folder, Action written, CancellationToken cancellationToken)
        {
            (long offset, long packed, int method) = await LocateAsync(client, cancellationToken);
            if (method != 8)
            {
                throw new InvalidDataException($"{Entry} is stored in the zip in a way JuliaSynth can't read (method {method}).");
            }

            // The entry: its local header (30 bytes, its name and an extra field), then the deflated archive.
            using HttpRequestMessage request = new(HttpMethod.Get, ZipUrl);
            request.Headers.Range = new RangeHeaderValue(offset, offset + 30 + 65_535 + 65_535 + packed);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            byte[] header = new byte[30];
            await body.ReadExactlyAsync(header, cancellationToken);
            if (BitConverter.ToUInt32(header, 0) != 0x04034b50)
            {
                throw new InvalidDataException($"The zip has no {Entry} where its list of files says.");
            }

            byte[] skipped = new byte[BitConverter.ToUInt16(header, 26) + BitConverter.ToUInt16(header, 28)];
            await body.ReadExactlyAsync(skipped, cancellationToken);
            await using DeflateStream archive = new(body, CompressionMode.Decompress);

            HashSet<string> left = [.. wanted];
            await using IAsyncReader reader = await ReaderFactory.OpenAsyncReader(archive, cancellationToken: cancellationToken);
            while (left.Count > 0 && await reader.MoveToNextEntryAsync(cancellationToken))
            {
                string name = Path.GetFileName((reader.Entry.Key ?? string.Empty).Replace('\\', '/'));
                if (reader.Entry.IsDirectory || !left.Remove(name))
                {
                    continue;
                }

                string path = Path.Combine(folder, name);
                if (new FileInfo(path) is not { Exists: true, Length: > 0 })
                {
                    string partial = path + ".part";
                    await using (FileStream file = File.Create(partial))
                    {
                        await reader.WriteEntryToAsync(file, cancellationToken);
                    }

                    File.Move(partial, path, overwrite: true);
                }

                written();
            }

            if (left.Count > 0)
            {
                throw new InvalidDataException($"{Entry} has no {left.Order(StringComparer.Ordinal).First()}{(left.Count > 1 ? $" (and {left.Count - 1} more)" : string.Empty)}.");
            }
        }

        /// <summary>
        /// Where <see cref="Entry"/> starts in the zip, how long it is packed, and how: from the zip's list of
        /// entries at its end.
        /// </summary>
        private async Task<(long Offset, long Packed, int Method)> LocateAsync(HttpClient client, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, ZipUrl);
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

            throw new InvalidDataException($"The zip at {ZipUrl} has no {Entry}.");
        }
    }
}
