using JSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.ViewModels
{
    /// <summary>
    /// The bearing examples: NASA's IMS bearings run to failure, a snapshot at a time and all at once.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// The snapshots of the second IMS bearing test, named by the time: one every 10 minutes from 12 February
        /// 2004, 10:32:39, until bearing 1 failed on 19 February.
        /// </summary>
        private static readonly string[] BearingSnapshots = [.. Enumerable.Range(0, 984)
            .Select(i => new DateTime(2004, 2, 12, 10, 32, 39).AddMinutes(10 * i).ToString("yyyy.MM.dd.HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture))];

        /// <summary>
        /// The second IMS bearing test, from NASA's zip of all three: every 8th snapshot and the last, or all 984.
        /// </summary>
        internal static ExampleData ImsBearings { get; } = new(
            "IMS bearings",
            "Vibration of four bearings on a shaft turning at 2,000 RPM under a 6,000 lb load until one of them failed: the "
                + "second test-to-failure of the IMS bearing data, 12 to 19 February 2004. Every 10 minutes, a 1-second snapshot "
                + "from an accelerometer on each bearing: 20,480 readings at 20 kHz, a column a bearing, in a text file named by "
                + "the time. Bearing 1's outer race failed at the end. The test's 86 MB archive is downloaded from NASA's zip of "
                + "all three tests, and unpacked.",
            "IMS bearing data from the NSF I/UCR Center for Intelligent Maintenance Systems (IMS), University of Cincinnati, "
                + "with support from Rexnord Corp.: J. Lee, H. Qiu, G. Yu, J. Lin, and Rexnord Technical Services (2007), "
                + "\"Bearing Data Set\", NASA Prognostics Data Repository, NASA Ames Research Center. A U.S. Government work "
                + "(public domain): https://data.nasa.gov/dataset/ims-bearings. Reference: H. Qiu, J. Lee, J. Lin, \"Wavelet "
                + "filter-based weak signature detection method and its application on roller bearing prognostics\", Journal "
                + "of Sound and Vibration 289 (2006) 1066-1090. The snapshots are unchanged.",
            "https://data.nasa.gov/docs/legacy/IMS.zip",
            [
                new("Short",
                    "Every 8th snapshot, one every 80 minutes, and the last: 69 MB once unpacked. Quick to run.",
                    [.. BearingSnapshots.Where((_, i) => i % 8 == 0), BearingSnapshots[^1]],
                    86_000_000),
                new("Full",
                    "All 984 snapshots, one every 10 minutes: 519 MB once unpacked.",
                    BearingSnapshots,
                    86_000_000),
            ])
        {
            Archive = new("https://data.nasa.gov/docs/legacy/IMS.zip", "IMS/2nd_test.rar"),
        };

        /// <summary>
        /// The bearings' snapshots gone through one at a time by <paramref name="files"/>: each bearing's strength and
        /// spikiness saved to <paramref name="rows"/> a row a snapshot, its strength over the last 6 snapshots compared
        /// with its strength when new, and an alarm noting the first time one gets 10% stronger. Snapshots of a
        /// stopped rig are passed over.
        /// </summary>
        private static MainViewModel.DesignDocument BearingSnapshotsGoneThrough(FilesNode files, string rows)
        {
            (files.Row, files.Column) = (6, Inputs);
            Function running = Define("running", "Bool", 6, Stage1,
                "sqrt(sum(abs2, X) / length(X)) > 0.01  # a stopped rig reads close to 0",
                ("X", "Matrix{Float64}"));
            WhenNode whileRunning = new() { Row = 6, Column = Stage2 };

            // Each bearing's strength (RMS) and spikiness (kurtosis), saved a row a snapshot.
            Function strength = Define("strength", "Vector{Float64}", 6, Stage3,
                "vec(sqrt.(sum(abs2, X; dims = 1) ./ size(X, 1)))  # each bearing's RMS vibration",
                ("X", "Matrix{Float64}"));
            Function spikiness = Define("spikiness", "Vector{Float64}", 17, Stage3,
                """
                # Each bearing's kurtosis: 3 for smooth vibration, more as impacts (a defect rolled over) come.
                map(eachcol(X)) do x
                    d = x .- sum(x) / length(x)
                    length(x) * sum(d .^ 4) / sum(d .^ 2)^2
                end
                """,
                ("X", "Matrix{Float64}"));
            Function row = Define("row", "Vector{Float64}", 17, Stage3 + 14, "[r; k]  # strengths, then spikiness: bearings 1 to 4", ("r", "Vector{Float64}"), ("k", "Vector{Float64}"));
            SaveFile saved = new() { Path = rows, PerFile = true, Row = 17, Column = Stage3 + 28 };

            // The strength over the last snapshots, smoothing out one-off spikes.
            WindowNode last = new() { Size = 6, Row = 6, Column = Stage3 + 14 };
            Function smooth = Define("smooth", "Vector{Float64}", 6, Stage3 + 28, "sum(W) / length(W)  # each bearing's mean strength over the window", ("W", "Vector"));

            // The strength when new: the mean of the first 12 running snapshots, then kept.
            Integer seen = Whole(0, 29);
            Function countUp = Define("count_up", "Int64", 29, Stage1, "k + 1  # the number of this running snapshot", ("k", "Int64"));
            ArrayBuilder baseline = Empty(JuliaTypes.Vector, "Float64", 40);
            Function learn = Define("learn", "Vector{Float64}", 40, Stage3 + 14,
                """
                # Each bearing's strength when new: the mean of the first 12 running snapshots, then kept.
                k > 12 ? B : isempty(B) ? r : B .+ (r .- B) ./ k
                """,
                ("B", "Vector{Float64}"), ("k", "Int64"), ("r", "Vector{Float64}"));

            // Wear: the most worn bearing's strength over its strength when new, plotted snapshot by snapshot.
            Function wear = Define("wear", "Float64", 29, Stage3 + 42, "maximum(S ./ B)  # the most worn bearing: its strength now over when new", ("S", "Vector{Float64}"), ("B", "Vector{Float64}"));
            ArrayBuilder trend = Empty(JuliaTypes.Vector, "Float64", 52);
            Function grow = Define("grow", "Vector{Float64}", 52, Stage3 + 56, "[T; w]  # the wear of every snapshot so far", ("T", "Vector{Float64}"), ("w", "Float64"));
            PlotNode plot = new() { Title = "Wear: the most worn bearing's strength over when new", Kind = "Line", Row = 50, Column = Stage3 + 74 };

            // The alarm: 10% stronger than when new; the first one noted, with when and which bearing.
            Float trigger = InPanel(Scalar(1.1, 29), "alarm at × when new", 1.02, 2, 1, 10);
            trigger.Column = Stage3 + 42;
            Function alarm = Define("alarm", "Bool", 29, Stage3 + 56, "w > t  # t times as strong as when new: 1.1, 10% stronger", ("w", "Float64"), ("t", "Float64"));
            Integer alarms = Whole(0, 64);
            Function alarmCount = Define("alarm_count", "Int64", 64, Stage3 + 70, "c + a  # the alarms so far", ("c", "Int64"), ("a", "Bool"));
            Function firstAlarm = Define("first_alarm", "Bool", 29, Stage3 + 70, "a && c == 0  # the first alarm only", ("a", "Bool"), ("c", "Int64"));
            (Node, string) name = (files, FilesNode.NameOutput);
            Function note = Define("note", Function.AnyType, 17, Stage3 + 56,
                """
                b = argmax(S ./ B)
                "$name: bearing $b, $(round(Int, 100 * (S[b] / B[b] - 1)))% stronger than when new"
                """,
                ("name", Function.AnyType), ("S", "Vector{Float64}"), ("B", "Vector{Float64}"));
            WhenNode noted = new() { Row = 17, Column = Stage3 + 70 };
            DisplayNode shown = Shown(17, Stage3 + 86);

            // In bands, a frame's each: what each snapshot gives, the strength now and when new, the alarm.
            Place((files, 6, Inputs), (running, 6, Stage1), (whileRunning, 6, Stage2),
                (strength, 6, Stage3), (spikiness, 17, Stage3), (row, 17, Stage3 + 14), (saved, 17, Stage3 + 28),
                (seen, 36, Inputs), (countUp, 36, Stage1), (last, 36, Stage2), (smooth, 36, Stage3), (baseline, 47, Inputs), (learn, 47, Stage2),
                (wear, 36, Stage3 + 20), (trend, 47, Stage3 + 20), (grow, 47, Stage3 + 34), (plot, 42, Stage3 + 52),
                (trigger, 64, Inputs), (alarm, 64, Stage1), (firstAlarm, 64, Stage2), (note, 64, Stage3), (noted, 64, Stage3 + 14),
                (shown, 64, Stage3 + 28), (alarms, 75, Inputs), (alarmCount, 75, Stage2));

            FrameNode runningOnly = Framed("Only when running", "Snapshots of the rig stopped are passed over.", running, whileRunning);
            FrameNode features = Framed("Strength and spikiness",
                "Each bearing's RMS vibration and kurtosis, saved a row a snapshot.", strength, spikiness, row, saved);
            FrameNode compared = Framed("Now, and when new",
                "smooth: each bearing's strength over the last 6 snapshots, so one-off spikes don't set the alarm off. learn: its "
                + "strength when new, from the first 12 running snapshots.",
                last, smooth, seen, countUp, baseline, learn);
            FrameNode worn = Framed("Wear", "The most worn bearing's strength over when new, snapshot by snapshot.", wear, trend, grow, plot);
            FrameNode alarmed = Framed("The first alarm",
                "Stronger than when new by the slider's times (on the left) is an alarm: the first noted, with when and which bearing.",
                trigger, alarm, alarms, alarmCount, firstAlarm, note, noted, shown);

            return Design(
                [files, runningOnly, features, compared, worn, alarmed, running, whileRunning, strength, spikiness, row, saved, last,
                    smooth, seen, countUp, baseline, learn, wear, trend, grow, plot, trigger, alarm, alarms, alarmCount, firstAlarm,
                    note, noted, shown],
                (files, running, 0), (files, whileRunning, "value"), (running, whileRunning, "condition"),
                (whileRunning, strength, 0), (whileRunning, spikiness, 0), (strength, row, 0), (spikiness, row, 1), (row, saved, 0),
                (strength, last, 0), (last, smooth, 0),
                (seen, countUp, 0), (countUp, seen, "next"), (baseline, learn, 0), (countUp, learn, 1), (strength, learn, 2), (learn, baseline, "next"),
                (smooth, wear, 0), (learn, wear, 1), (trend, grow, 0), (wear, grow, 1), (grow, trend, "next"), (grow, plot, "y"),
                (wear, alarm, 0), (trigger, alarm, 1), (alarms, alarmCount, 0), (alarm, alarmCount, 1), (alarmCount, alarms, "next"),
                (alarm, firstAlarm, 0), (alarms, firstAlarm, 1),
                (name, note, 0), (smooth, note, 1), (learn, note, 2), (note, noted, "value"), (firstAlarm, noted, "condition"), (noted, shown, 0));
        }

        private static MainViewModel.DesignDocument BearingWear()
        {
            // The full set when it was downloaded, else the short one, a snapshot at a time: 6 snapshots an hour,
            // or the short set's 6 (8 hours).
            string folder = ImsBearings.DownloadedFolder ?? ExampleData.SetFolder(ImsBearings.Folder, ImsBearings.Sets[0]);
            FilesNode files = new() { Format = FileFormats.Tsv, Pattern = "2004.*", Folder = folder, EachFile = true };
            return BearingSnapshotsGoneThrough(files, System.IO.Path.Combine(ImsBearings.Folder, "bearing-wear.csv"));
        }

        private static MainViewModel.DesignDocument BearingWeekInParallel()
        {
            // The full set when it was downloaded, else the short one: all the snapshots at once, in parallel.
            string folder = ImsBearings.DownloadedFolder ?? ExampleData.SetFolder(ImsBearings.Folder, ImsBearings.Sets[0]);
            FilesNode files = new() { Format = FileFormats.Tsv, Pattern = "2004.*", Folder = folder, Row = 6, Column = Inputs };

            // Each snapshot's features, worked out on every core at once: the snapshots don't depend on each other.
            Function features = Define("features", "Vector{Float64}", 20, Inputs,
                """
                # Each bearing's strength (RMS vibration), then its spikiness (kurtosis: 3 for smooth vibration, more as
                # impacts, a defect rolled over, come).
                strength = vec(sqrt.(sum(abs2, X; dims = 1) ./ size(X, 1)))
                spikiness = map(eachcol(X)) do x
                    d = x .- sum(x) / length(x)
                    length(x) * sum(d .^ 4) / sum(d .^ 2)^2
                end
                [strength; spikiness]
                """,
                ("X", "Matrix{Float64}"));
            MapNode each = new() { InParallel = true, Row = 8, Column = Stage1 };
            Function table = Define("table", "Matrix{Float64}", 6, Stage2,
                "permutedims(reduce(hcat, F))  # a row a snapshot: bearings 1 to 4's strength, then their spikiness",
                ("F", "Vector"));
            SaveFile saved = new() { Path = System.IO.Path.Combine(ImsBearings.Folder, "bearing-features.csv"), Row = 20, Column = Stage2 };

            // Wear: each bearing's strength over when new, smoothed over the last 6 snapshots.
            Function wear = Define("wear", "Matrix{Float64}", 6, Stage3,
                """
                # Each bearing's strength over when new (the mean of the first 12 snapshots), a row a snapshot, smoothed over
                # the last 6 (an hour of the full set). The snapshots of the rig stopped (close to 0) are left out.
                S = T[vec(maximum(T[:, 1:4]; dims = 2)) .> 0.01, 1:4]
                smooth = reduce(vcat, [sum(S[max(1, i - 5):i, :]; dims = 1) ./ length(max(1, i - 5):i) for i in axes(S, 1)])
                smooth ./ (sum(S[1:12, :]; dims = 1) ./ 12)
                """,
                ("T", "Matrix{Float64}"));
            Function byBearing = Define("by_bearing", "Matrix{Float64}", 6, Stage3 + 14, "permutedims(W)  # a row a bearing, a column a snapshot", ("W", "Matrix{Float64}"));
            PlotNode heat = new() { Title = "Each bearing's wear over the week (rows: bearings 1 to 4)", Kind = "Heatmap", Row = 6, Column = Stage3 + 30 };
            Function worst = Define("worst", "Vector{Float64}", 20, Stage3 + 14, "vec(maximum(W; dims = 2))  # the most worn bearing's, snapshot by snapshot", ("W", "Matrix{Float64}"));
            PlotNode line = new() { Title = "Wear: the most worn bearing's strength over when new", Kind = "Line", Row = 22, Column = Stage3 + 30 };

            // With every snapshot at hand, when each bearing wore for good: hindsight a stream doesn't have.
            Function onset = Define("onset", Function.AnyType, 34, Stage3,
                """
                # When each bearing got t times as strong as when new (1.1: 10% stronger) for good: after its last snapshot not above that. A stream
                # can only say it got stronger, not that it never calmed down again.
                names = basename.(files.paths)[vec(maximum(T[:, 1:4]; dims = 2)) .> 0.01]
                join(map(1:4) do b
                    i = something(findlast(<=(t), W[:, b]), 0)
                    i == size(W, 1) ? "bearing $b: not worn" :
                        "bearing $b: worn from $(names[i + 1]), $(size(W, 1) - i) snapshots before the end, at most $(round(maximum(W[:, b]); digits = 1))×"
                end, "\n")
                """,
                ("files", Function.AnyType), ("T", "Matrix{Float64}"), ("W", "Matrix{Float64}"), ("t", "Float64"));
            DisplayNode shown = Shown(34, Stage3 + 16);
            Float worn = InPanel(Scalar(1.1, 34), "worn at × when new", 1.02, 2, 1, 10);
            worn.Column = Stage3 - 14;

            // In bands: the snapshots and their features, then what they say in hindsight.
            Place((files, 6, Inputs), (each, 8, Stage1), (features, 19, Stage1), (table, 8, Stage2), (saved, 19, Stage2),
                (wear, 8, Stage3 + 4), (byBearing, 8, Stage3 + 18), (heat, 8, Stage3 + 34), (worst, 19, Stage3 + 18), (line, 24, Stage3 + 34),
                (worn, 48, Inputs), (onset, 48, Stage1), (shown, 48, Stage2));

            FrameNode inParallel = Framed("Every snapshot at once",
                "The Map applies features to every snapshot on all of Julia's threads (∥ switches it to one at a time); table "
                + "holds them a row a snapshot, saved to bearing-features.csv.",
                features, each, table, saved);
            FrameNode wearing = Framed("Wear", "Each bearing's strength over when new, smoothed: a heatmap of the four, a line of the most worn.",
                wear, byBearing, heat, worst, line);
            FrameNode hindsight = Framed("Hindsight",
                "With the whole week at hand, when each bearing got stronger for good (by the slider's times, on the left).",
                worn, onset, shown);

            return Design(
                [files, inParallel, wearing, hindsight, features, each, table, saved, wear, byBearing, heat, worst, line, worn, onset, shown],
                (files, each, "items"), (features, each, "f"), (each, table, 0), (table, saved, 0),
                (table, wear, 0), (wear, byBearing, 0), (byBearing, heat, "y"), (wear, worst, 0), (worst, line, "y"),
                (files, onset, 0), (table, onset, 1), (wear, onset, 2), (worn, onset, 3), (onset, shown, 0));
        }
    }
}
