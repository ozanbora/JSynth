using JuliaSynth.Model;
using JuliaSynth.Runtime;
using JuliaSynth.ViewModels;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JuliaSynth.Tests.Arrays
{
    /// <summary>
    /// PNG images read as matrices of grey levels, by Load from file and the Files node.
    /// </summary>
    public sealed class PngTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JuliaSynth.Tests", Guid.NewGuid().ToString("N"));

        public PngTests()
        {
            _ = Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// Writes a grey PNG of <paramref name="pixels"/> (rows top first), 8 or 16 bits deep.
        /// </summary>
        internal static void WriteGreyPng(string path, int[,] pixels, int bitDepth)
        {
            int rows = pixels.GetLength(0), columns = pixels.GetLength(1), bytes = bitDepth / 8;
            using MemoryStream raw = new();
            for (int r = 0; r < rows; r++)
            {
                raw.WriteByte(0); // no filter
                for (int c = 0; c < columns; c++)
                {
                    if (bytes == 2)
                    {
                        raw.WriteByte((byte)(pixels[r, c] >> 8));
                    }

                    raw.WriteByte((byte)pixels[r, c]);
                }
            }

            using MemoryStream compressed = new();
            using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(zlib);
            }

            byte[] header = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)columns);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)rows);
            header[8] = (byte)bitDepth; // colour type 0 (grey), then compression, filter and interlace 0

            using FileStream file = File.Create(path);
            file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
            Chunk(file, "IHDR", header);
            Chunk(file, "IDAT", compressed.ToArray());
            Chunk(file, "IEND", []);
        }

        private static void Chunk(Stream file, string type, byte[] data)
        {
            byte[] length = new byte[4], crc = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            byte[] typed = [.. type.Select(c => (byte)c), .. data];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typed));
            file.Write(length);
            file.Write(typed);
            file.Write(crc);
        }

        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
                }
            }

            return ~crc;
        }

        [Fact]
        public void Png_is_a_format_of_both_file_nodes()
        {
            Assert.Equal(FileFormats.Png, FileFormats.OfExtension(".PNG"));
            Assert.Equal("*.png", FileFormats.Pattern(FileFormats.Png));
            Assert.Equal([JuliaTypes.Matrix], FileFormats.ShapesFor(FileFormats.Png));
            Assert.Equal("PNGFiles", FileFormats.Package(FileFormats.Png));
            Assert.Equal(["ColorTypes"], FileFormats.Imports(FileFormats.Png));

            Assert.Equal("Matrix{Float64}(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load(path))))",
                FileFormats.Read(FileFormats.Png, "path", JuliaTypes.Matrix, "Float64", false, string.Empty));
            Assert.Equal("Matrix{UInt16}(reinterpret.(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load(path)))))",
                FileFormats.Read(FileFormats.Png, "path", JuliaTypes.Matrix, "UInt16", false, string.Empty));
        }

        [Fact]
        public void A_png_file_says_its_size_and_depth_and_whether_the_type_holds_its_pixels()
        {
            string path = Path.Combine(_folder, "frame.png");
            WriteGreyPng(path, new int[3, 5], 16);

            ArrayFile file = new() { Path = path };
            Assert.Equal(FileFormats.Png, file.Format);
            Assert.Equal(JuliaTypes.Matrix, file.Shape);
            Assert.True(file.IsImage);
            Assert.Equal("Matrix{Float64}", file.OutputType);
            Assert.Equal("3×5, 16-bit grey", file.Summary);
            Assert.Null(file.Error);

            file.ElementType = "UInt8";
            Assert.Equal("Pixels of this image go up to 65535: pick an integer type holding that, or a float type for intensities from 0 to 1.", file.Error);
            file.ElementType = "UInt16";
            Assert.Null(file.Error);

            string fake = Path.Combine(_folder, "fake.png");
            File.WriteAllText(fake, "not an image");
            file.Path = fake;
            Assert.Equal("This is not a PNG image.", file.Error);
        }

        [Fact]
        public void Png_packages_are_loaded_so_that_their_names_stay_out_of_the_design()
        {
            Ui.Run(() =>
            {
                new MainViewModel().NewDesign();
                MainViewModel.Nodes.Add(new ArrayFile { Path = "frame.png" });

                JuliaScript run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);
                string exported = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;

                Assert.Equal(["ColorTypes", "PNGFiles"], run.Packages);
                Assert.Contains("(\"PNGFiles\", \"using\"), (\"ColorTypes\", \"import\")", run.Text);
                Assert.Contains("using PNGFiles\nimport ColorTypes\n", exported);
            });
        }

        [Fact]
        public void Real_julia_reads_a_folder_of_frames_as_grey_levels_or_stored_values()
        {
            Ui.Run(async () =>
            {
                // Needs Julia with PNGFiles and ColorTypes, which nothing here installs.
                if (await JuliaLocator.FindAsync(null) is not JuliaInstallation julia
                    || (await new JuliaPackages(julia.Path).FindMissingAsync(["PNGFiles", "ColorTypes"], _folder)).Count > 0)
                {
                    return;
                }

                WriteGreyPng(Path.Combine(_folder, "frame-1.png"), new[,] { { 0, 1000 }, { 65535, 7 } }, 16);
                WriteGreyPng(Path.Combine(_folder, "frame-2.png"), new[,] { { 10, 20, 30 } }, 8);

                new MainViewModel().NewDesign();
                FilesNode files = new() { Folder = _folder, Format = FileFormats.Png, ElementType = "Int64" };
                ArrayFile intensity = new() { Path = Path.Combine(_folder, "frame-2.png") };
                MainViewModel.Nodes.AddRange([files, intensity]);
                JuliaScript script = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true);

                using JuliaSession session = new(julia.Path);
                ConcurrentQueue<Report> reports = new();
                ConcurrentQueue<OutputLine> output = new();
                RunResult result = await session.RunAsync(script.Text + "\nforeach(println, " + script.Variables[files.ID] + ")", _folder, output.Enqueue, Finished.Into(reports));

                Assert.True(result.Succeeded);
                Assert.Equal("1×3 Matrix{Float64}:\n 0.0392157  0.0784314  0.117647", reports.Single(r => r.Id == intensity.ID).Text);
                Assert.Contains("2 files", reports.Single(r => r.Id == files.ID).Text);

                // Each frame of the folder, in name order, as the values stored: 16 and 8-bit alike.
                Assert.Equal(new[] { "[0 1000; 65535 7]", "[10 20 30]" }, output.Select(l => l.Text));
            });
        }
    }
}
