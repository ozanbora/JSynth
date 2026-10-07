using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// The video example: what moves in the frames of a highway camera, from CDnet 2014.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// The frames of CDnet's highway video and their marked twins, by number: in000001.png and gt000001.png.
        /// </summary>
        private static string[] HighwayFrames(int count) => [.. Enumerable.Range(1, count).SelectMany(i => new[] { $"in{i:000000}.png", $"gt{i:000000}.png" })];

        /// <summary>
        /// The highway video of the CDnet 2014 change detection dataset, from its own archive: the frames turned to
        /// grey PNG as they are unpacked, with the frames marked by hand.
        /// </summary>
        internal static ExampleData CdnetHighway { get; } = new(
            "CDnet highway",
            "A fixed camera over a highway, 1,700 frames of 320×240, from the CDnet 2014 change detection dataset: each "
                + "frame comes with a twin marked by hand, white where something moves, black where nothing does (and grey "
                + "for shadows and what is unsure), to score a method by. Frames 470 to 1,700 are scored; the ones before "
                + "are for a method to learn the scene. The video's 28 MB archive is downloaded and unpacked, its JPEG "
                + "frames turned to grey PNG.",
            "From the CDnet 2014 change detection dataset (www.changedetection.net): Y. Wang, P.-M. Jodoin, F. Porikli, "
                + "J. Konrad, Y. Benezeth, P. Ishwar, \"CDnet 2014: An Expanded Change Detection Benchmark Dataset\", CVPR "
                + "Workshops 2014; the highway video is of its baseline category: N. Goyette, P.-M. Jodoin, F. Porikli, "
                + "J. Konrad, P. Ishwar, \"changedetection.net: A new change detection benchmark dataset\", CVPR Workshops "
                + "2012. The frames are turned from JPEG to grey PNG; those marked by hand are unchanged.",
            "https://changedetection.net/static/dataset/baseline/highway.zip",
            [
                new("Short",
                    "The first 800 frames: 469 to learn the scene, then 331 scored. 36 MB once unpacked.",
                    HighwayFrames(800),
                    28_000_000),
                new("Full",
                    "All 1,700 frames, 1,231 of them scored. 78 MB once unpacked.",
                    HighwayFrames(1700),
                    28_000_000),
            ])
        {
            Archive = new("https://changedetection.net/static/dataset/baseline/highway.zip", null)
            {
                // highway/input/in000001.jpg becomes in000001.png, a grey PNG; highway/groundtruth/gt000001.png stays.
                Rename = key => System.IO.Path.GetFileName(key) is string name && (key.Contains("/input/") || key.Contains("/groundtruth/"))
                    ? System.IO.Path.ChangeExtension(name, ".png")
                    : null,
                Convert = (key, from, to) =>
                {
                    if (key.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        ExampleArchive.GreyPng(from, to);
                    }
                    else
                    {
                        from.CopyTo(to);
                    }
                },
            },
        };

        private static MainViewModel.DesignDocument MotionInVideo()
        {
            // The full set when it was downloaded, else the short one, a frame at a time.
            string folder = CdnetHighway.DownloadedFolder ?? ExampleData.SetFolder(CdnetHighway.Folder, CdnetHighway.Sets[0]);
            FilesNode frames = new() { Format = FileFormats.Png, Pattern = "in*.png", Folder = folder, EachFile = true, Row = 6, Column = Inputs };

            // The background: the scene without what moves, following it slowly where nothing moves.
            ArrayBuilder background = Empty(JuliaTypes.Matrix, "Float64", 18);
            Function moving = Define("moving", "Matrix{Float64}", 6, Stage1,
                """
                # 1 where the frame is far from the background (something moves), 0 elsewhere: nothing on the first frame.
                isempty(B) ? zeros(size(X)) : Float64.(abs.(X .- B) .> 0.12)
                """,
                ("X", "Matrix{Float64}"), ("B", "Matrix{Float64}"));
            Function update = Define("update", "Matrix{Float64}", 18, Stage2,
                """
                # The background follows the scene, 5% a frame, but only where nothing moves: a passing car doesn't
                # smear into it. (Updating everywhere scores F 0.73 instead of 0.84: try it.)
                isempty(B) ? X : ifelse.(M .> 0, B, 0.95 .* B .+ 0.05 .* X)
                """,
                ("B", "Matrix{Float64}"), ("X", "Matrix{Float64}"), ("M", "Matrix{Float64}"));
            PlotNode frame = new() { Title = "The frame", Kind = "Heatmap", Row = 7, Column = Stage3 + 2 };
            PlotNode mask = new() { Title = "Moving: 1, still: 0", Kind = "Heatmap", Row = 7, Column = Stage3 + 20 };
            SaveFile saved = new() { Path = System.IO.Path.Combine(CdnetHighway.Folder, "masks", "mask.png"), PerFile = true, Row = 21, Column = Stage3 + 20 };

            // Scored against the frame's twin, marked by hand: pixels found moving that do, that don't, and those missed.
            (Node, string) path = (frames, FilesNode.PathOutput);
            Function twin = Define("twin", "Matrix{Float64}", 32, Stage1,
                """
                using PNGFiles
                import ColorTypes
                # The frame's twin marked by hand, in the same folder: in000123.png's gt000123.png. 1 moving, 0 still;
                # in between, shadows (0.2), outside the scored area (0.33) and unsure (0.67).
                Float64.(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load(joinpath(dirname(p), "gt" * basename(p)[3:end])))))
                """,
                ("p", Function.AnyType));
            Function score = Define("score", "Vector{Float64}", 32, Stage2,
                """
                # Pixels found moving that move, found moving that are still, and moving but missed, from frame 470 on
                # (the highway video's scored frames: those before are for learning the scene).
                parse(Int, basename(p)[3:8]) < 470 && return zeros(3)
                found, moves, still = M .> 0, G .> 0.9, G .< 0.1
                Float64[count(found .& moves), count(found .& still), count(.!found .& moves)]
                """,
                ("M", "Matrix{Float64}"), ("G", "Matrix{Float64}"), ("p", Function.AnyType));
            ArrayBuilder totals = Empty(JuliaTypes.Vector, "Float64", 44);
            foreach (Argument size in totals.Parameters.OfType<Argument>().Where(a => a.Key == "n"))
            {
                size.Value = "3";
            }

            Function add = Define("add", "Vector{Float64}", 44, Stage3, "T .+ s  # the counts of every frame so far", ("T", "Vector{Float64}"), ("s", "Vector{Float64}"));
            Function fScore = Define("f_score", Function.AnyType, 44, Stage3 + 14,
                """
                # Precision (found moving that do), recall (moving found) and F, their harmonic mean: 1 is perfect.
                hits, wrong, missed = T
                p, r = hits / (hits + wrong), hits / (hits + missed)
                (precision = round(p; digits = 3), recall = round(r; digits = 3), F = round(2p * r / (p + r); digits = 3))
                """,
                ("T", "Vector{Float64}"));
            DisplayNode shown = Shown(44, Stage3 + 30);

            return Design(
                [frames, background, moving, update, frame, mask, saved, twin, score, totals, add, fScore, shown],
                (frames, moving, 0), (background, moving, 1), (background, update, 0), (frames, update, 1), (moving, update, 2), (update, background, "next"),
                (frames, frame, "y"), (moving, mask, "y"), (moving, saved, 0),
                (path, twin, 0), (moving, score, 0), (twin, score, 1), (path, score, 2), (totals, add, 0), (score, add, 1), (add, totals, "next"),
                (add, fScore, 0), (fScore, shown, 0));
        }
    }
}
