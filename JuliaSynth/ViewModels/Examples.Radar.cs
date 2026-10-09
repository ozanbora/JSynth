using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// The radar examples: the Boreas scans of a Navtech radar, a clutter map and what stands out of it.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// The scans of the Boreas dataset's radar while its car stood still for 53 s, one every quarter second.
        /// </summary>
        private static readonly long[] StopScans =
        [
            1606417888039363, 1606417888289369, 1606417888539372, 1606417888789377, 1606417889039375, 1606417889290004,
            1606417889540012, 1606417889790012, 1606417890040020, 1606417890289392, 1606417890539398, 1606417890789406,
            1606417891039412, 1606417891289407, 1606417891539413, 1606417891789415, 1606417892040043, 1606417892290048,
            1606417892540051, 1606417892790064, 1606417893039433, 1606417893289440, 1606417893539442, 1606417893789448,
            1606417894039447, 1606417894289447, 1606417894539456, 1606417894790087, 1606417895039463, 1606417895289463,
            1606417895539466, 1606417895789474, 1606417896039477, 1606417896289487, 1606417896539483, 1606417896789487,
            1606417897039495, 1606417897289498, 1606417897538869, 1606417897789495, 1606417898039510, 1606417898290140,
            1606417898540140, 1606417898790762, 1606417899040761, 1606417899290776, 1606417899540154, 1606417899790155,
            1606417900040155, 1606417900290157, 1606417900540164, 1606417900790177, 1606417901039544, 1606417901289548,
            1606417901539558, 1606417901788937, 1606417902038936, 1606417902289561, 1606417902540190, 1606417902790201,
            1606417903040197, 1606417903290204, 1606417903539585, 1606417903789585, 1606417904038961, 1606417904288973,
            1606417904539598, 1606417904789597, 1606417905040227, 1606417905290857, 1606417905540239, 1606417905790244,
            1606417906040239, 1606417906290242, 1606417906539626, 1606417906789632, 1606417907039631, 1606417907289635,
            1606417907539640, 1606417907789646, 1606417908040302, 1606417908290895, 1606417908540277, 1606417908790288,
            1606417909040290, 1606417909290292, 1606417909540294, 1606417909790296, 1606417910040302, 1606417910289682,
            1606417910539683, 1606417910789688, 1606417911039688, 1606417911289070, 1606417911539072, 1606417911789701,
            1606417912039700, 1606417912290339, 1606417912540340, 1606417912790339, 1606417913040347, 1606417913290345,
            1606417913539726, 1606417913789728, 1606417914039738, 1606417914289743, 1606417914539739, 1606417914789741,
            1606417915039753, 1606417915289749, 1606417915539757, 1606417915789755, 1606417916039761, 1606417916289767,
            1606417916539767, 1606417916789161, 1606417917039769, 1606417917289782, 1606417917540410, 1606417917790409,
            1606417918040409, 1606417918290411, 1606417918539795, 1606417918789799, 1606417919039805, 1606417919289176,
            1606417919539808, 1606417919789817, 1606417920039820, 1606417920289828, 1606417920540451, 1606417920789824,
            1606417921039831, 1606417921289833, 1606417921539840, 1606417921789840, 1606417922039845, 1606417922289849,
            1606417922539853, 1606417922790486, 1606417923039856, 1606417923289872, 1606417923539868, 1606417923789875,
            1606417924040502, 1606417924290499, 1606417924540501, 1606417924789892, 1606417925039887, 1606417925289889,
            1606417925539892, 1606417925789901, 1606417926039904, 1606417926289912, 1606417926539909, 1606417926789911,
            1606417927039922, 1606417927289920, 1606417927539299, 1606417927789927, 1606417928039930, 1606417928290570,
            1606417928541187, 1606417928790568, 1606417929040569, 1606417929290571, 1606417929540581, 1606417929790582,
            1606417930040583, 1606417930289963, 1606417930539965, 1606417930789968, 1606417931039974, 1606417931289977,
            1606417931540605, 1606417931790605, 1606417932039983, 1606417932289991, 1606417932539994, 1606417932790009,
            1606417933040001, 1606417933290001, 1606417933540011, 1606417933790019, 1606417934040012, 1606417934290014,
            1606417934540026, 1606417934790023, 1606417935040030, 1606417935290030, 1606417935540034, 1606417935790046,
            1606417936040672, 1606417936290676, 1606417936540049, 1606417936790056, 1606417937040056, 1606417937290056,
            1606417937540692, 1606417937790696, 1606417938040079, 1606417938290072, 1606417938540076, 1606417938790079,
            1606417939040085, 1606417939290089, 1606417939540091, 1606417939790095, 1606417940040723, 1606417940290724,
            1606417940540731, 1606417940790118, 1606417941040743,        ];

        /// <summary>
        /// The Boreas radar scans of the stop: a short set of 12 spread over it (two of them with another
        /// radar's interference, scans 3 and 25 of the stop), or all 213.
        /// </summary>
        internal static ExampleData BoreasRadar { get; } = new(
            "Boreas radar scans",
            "Scans of a Navtech radar spinning on the roof of a car standing still for 53 seconds at a junction in Toronto, "
                + "on 26 November 2020, from the Boreas dataset. Each scan is a PNG image of 400 azimuths (rows) by 3,371 "
                + "columns: an 11-column timestamp, then 3,360 range bins of about 6 cm.",
            "From the Boreas dataset by the Autonomous Space Robotics Lab, University of Toronto, licensed under CC BY 4.0 "
                + "(https://creativecommons.org/licenses/by/4.0/): K. Burnett et al., \"Boreas: A multi-season autonomous "
                + "driving dataset\", The International Journal of Robotics Research, 2023. https://www.boreas.utias.utoronto.ca/ "
                + "The scans are unchanged.",
            "https://boreas.s3.us-west-2.amazonaws.com/boreas-2020-11-26-13-58/radar/",
            [
                new("Short",
                    "12 scans spread over the stop, two of them with another radar's interference: quick to download and run.",
                    [.. new[] { 1, 3, 20, 25, 40, 60, 80, 100, 120, 140, 160, 180 }.Select(i => $"{StopScans[i - 1]}.png")],
                    4_500_000),
                new("Full",
                    "All 213 scans of the stop, one every quarter second: traffic moves smoothly from scan to scan.",
                    [.. StopScans.Select(time => $"{time}.png")],
                    80_000_000),
            ]);

        /// <summary>
        /// top_down(C) of the radar examples: a radar scan's azimuth-by-range matrix seen from above.
        /// </summary>
        private const string TopDownBody =
                """
                # The clutter map seen from above, like a radar screen: the radar in the middle, its 0° up,
                # 0.3 m a pixel out to 60 m. Each pixel takes the strongest echo of the range bins (6 cm) it covers.
                n, metres, bin = 401, 0.3, 0.0596
                centre = (n + 1) ÷ 2
                image = zeros(n, n)
                for i in 1:n, j in 1:n
                    x, y = (j - centre) * metres, (centre - i) * metres
                    r = hypot(x, y)
                    bins = max(floor(Int, (r - metres / 2) / bin) + 1, 1):min(ceil(Int, (r + metres / 2) / bin), size(C, 2))
                    isempty(bins) && continue
                    a = mod(round(Int, atan(x, y) / 2π * size(C, 1)), size(C, 1)) + 1  # the azimuth, clockwise from up
                    image[i, j] = maximum(view(C, a, bins))
                end
                image
                """;

        private const string UpdateBody =
                """
                # The clutter map, the scene as it usually is: each scan moves it a little towards itself.
                α = 0.2  # how quickly the clutter map follows the scene
                isempty(C) ? X : (1 - α) .* C .+ α .* X  # the first scan starts it
                """;

        private const string StandsOutBody =
                """
                # 1 where this scan stands out from the clutter map as it was before it, 0 elsewhere. C has this
                # scan in it already, at α = 0.2 (see update): X - C is 0.8 of how far X stands out from the map before.
                Float64.((X .- C) ./ 0.8 .> 0.15)
                """;

        private const string InterferedBody =
                """
                # 1 when an azimuth of this scan stands out along most of its range, in 8 of its 10 stretches
                # of 100 bins (6 m each): another radar's signal. A vehicle covers a few metres. As in stands_out,
                # against the clutter map before this scan.
                lit(a) = count(b -> count(>(0.15), (X[a, b:b+99] .- C[a, b:b+99]) ./ 0.8) >= 10, 1:100:1000)
                Int(any(a -> lit(a) >= 8, axes(X, 1)))
                """;

        /// <summary>
        /// The radar scans gone through one at a time, from <paramref name="files"/> (a Files node so set, or a
        /// Watch folder): the clutter map, and what stands out from it, carried from scan to scan by inputs
        /// taking their next values. With <paramref name="list"/>, the scans with interference are listed,
        /// else counted; with <paramref name="profile"/>, the clutter's strength by range bin is plotted.
        /// </summary>
        private static MainViewModel.DesignDocument RadarScans(FilesNode files, string live, bool list, bool profile)
        {
            (files.Row, files.Column) = (6, Inputs);
            Function scan = Define("scan", "Matrix{Float64}", 6, Stage1,
                "X[:, 12:1011]  # without the 11 timestamp columns; the first 1000 range bins (about 60 m)",
                ("X", "Matrix{Float64}"));

            // The clutter map: empty at first, then each scan's update.
            ArrayBuilder clutter = Empty(JuliaTypes.Matrix, "Float64", 20);
            Function update = Define("update", "Matrix{Float64}", 20, Stage1, UpdateBody, ("C", "Matrix{Float64}"), ("X", "Matrix{Float64}"));
            Function topDown = Define("top_down", "Matrix{Float64}", 20, Stage2, TopDownBody, ("C", "Matrix{Float64}"));
            PlotNode clutterMap = new() { Title = $"Clutter map from above (0.3 m a pixel){live}", Kind = "Heatmap", Row = 21, Column = Stage3 + 2 };

            // How many scans each spot stood out in: empty at first, then each scan's count added.
            ArrayBuilder tally = Empty(JuliaTypes.Matrix, "Float64", 34);
            Function standsOut = Define("stands_out", "Matrix{Float64}", 34, Stage2, StandsOutBody, ("X", "Matrix{Float64}"), ("C", "Matrix{Float64}"));
            Function add = Define("add", "Matrix{Float64}", 34, Stage3, "isempty(T) ? S : T .+ S  # one more scan counted where a spot stood out",
                ("T", "Matrix{Float64}"), ("S", "Matrix{Float64}"));
            Function traffic = Define("traffic", "Matrix{Float64}", 34, Stage3 + 14,
                "top_down(min.(T, 3))  # from above, 3 scans or more brightest: what moves leaves tracks",
                ("T", "Matrix{Float64}"));
            PlotNode tracks = new() { Title = $"Standing out from the clutter, from above{live}", Kind = "Heatmap", Row = 35, Column = Stage3 + 30 };

            // Another radar's interference: 1 for a scan with it, counted, or listed by scan number.
            Function interfered = Define("interfered", "Int64", 48, Stage2, InterferedBody, ("X", "Matrix{Float64}"), ("C", "Matrix{Float64}"));

            List<Node> nodes = [files, scan, clutter, update, topDown, clutterMap, tally, standsOut, add, traffic, tracks, interfered];
            List<(Node, Node, object)> wires =
            [
                (files, scan, 0),
                (clutter, update, 0), (scan, update, 1), (update, clutter, "next"), (update, topDown, 0), (topDown, clutterMap, "y"),
                (scan, standsOut, 0), (update, standsOut, 1), (tally, add, 0), (standsOut, add, 1), (add, tally, "next"),
                (add, traffic, 0), (traffic, tracks, "y"),
                (scan, interfered, 0), (update, interfered, 1),
            ];

            List<Node> map = [clutter, update, topDown, clutterMap];
            List<Node> interference = [interfered];
            if (list)
            {
                Integer number = Whole(0, 48);
                Function countUp = Define("count_up", "Int64", 48, Stage1, "k + 1  # the number of this scan", ("k", "Int64"));
                ArrayBuilder hits = Empty(JuliaTypes.Vector, "Int64", 60);
                Function noted = Define("noted", "Vector{Int64}", 55, Stage3,
                    "hit == 1 ? [hits; k] : hits  # the scans with another radar's interference, by number",
                    ("hits", "Vector{Int64}"), ("k", "Int64"), ("hit", "Int64"));
                DisplayNode shown = Shown(55, Stage3 + 16);
                nodes.AddRange([number, countUp, hits, noted, shown]);
                interference.AddRange([number, countUp, hits, noted, shown]);
                wires.AddRange([(number, countUp, 0), (countUp, number, "next"), (hits, noted, 0), (countUp, noted, 1), (interfered, noted, 2), (noted, hits, "next"), (noted, shown, 0)]);
            }
            else
            {
                Integer count = Whole(0, 48);
                Function plus = Define("plus", "Int64", 48, Stage3, "n + hit  # one more scan with interference, or not", ("n", "Int64"), ("hit", "Int64"));
                DisplayNode shown = Shown(48, Stage3 + 16);
                nodes.AddRange([count, plus, shown]);
                interference.AddRange([count, plus, shown]);
                wires.AddRange([(count, plus, 0), (interfered, plus, 1), (plus, count, "next"), (plus, shown, 0)]);
            }

            if (profile)
            {
                Function rangeProfile = Define("range_profile", "Vector{Float64}", 27, Stage2,
                    "vec(sum(C; dims = 1)) ./ size(C, 1)  # the clutter's mean strength at each range bin",
                    ("C", "Matrix{Float64}"));
                PlotNode plot = new() { Title = "Clutter strength by range bin", Row = 35, Column = Stage3 + 2 };
                nodes.AddRange([rangeProfile, plot]);
                map.AddRange([rangeProfile, plot]);
                wires.AddRange([(update, rangeProfile, 0), (rangeProfile, plot, "y")]);
            }

            nodes.InsertRange(1, [
                Framed("The clutter map", "The scene as it usually is: empty at first, each scan moving it a little towards itself (update), "
                    + "the input taking update as its next value. top_down shows it from above, like a radar screen.", [.. map]),
                Framed("What stands out: the traffic", "Each spot that stood out from the clutter map, counted scan by scan: the tracks of "
                    + "what moves.", tally, standsOut, add, traffic, tracks),
                Framed("Another radar's interference", list
                    ? "A scan with one azimuth standing out along most of its range: another radar's signal, listed by scan number."
                    : "A scan with one azimuth standing out along most of its range: another radar's signal, counted.", [.. interference]),
            ]);
            return Design([.. nodes], [.. wires]);
        }

        private static MainViewModel.DesignDocument RadarClutterMap()
        {
            // The full set when it was downloaded, else the short one, a scan at a time.
            string folder = BoreasRadar.DownloadedFolder ?? ExampleData.SetFolder(BoreasRadar.Folder, BoreasRadar.Sets[0]);
            return RadarScans(new FilesNode { Format = FileFormats.Png, Folder = folder, EachFile = true }, string.Empty, list: true, profile: true);
        }

        private static MainViewModel.DesignDocument LiveRadar()
        {
            // The scans are replayed, 4 a second as the radar made them, into a folder of their own that the
            // design watches: the full set when it was downloaded, else the short one.
            string live = System.IO.Path.Combine(BoreasRadar.Folder, "live");
            try
            {
                _ = System.IO.Directory.CreateDirectory(live);
            }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
            {
                // The Watch folder node says the folder is missing.
            }

            WatchNode watch = new()
            {
                Format = FileFormats.Png,
                Folder = live,
                ReplayFolder = BoreasRadar.DownloadedFolder ?? ExampleData.SetFolder(BoreasRadar.Folder, BoreasRadar.Sets[0]),
                ReplayRate = 4,
            };

            return RadarScans(watch, ", live", list: false, profile: false);
        }
    }
}
