using JSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.ViewModels
{
    /// <summary>
    /// A ready-made design showing what JSynth is for.
    /// </summary>
    /// <param name="Name">Shown in the Examples menu.</param>
    /// <param name="Description">What it computes and what to try; shown when it is opened.</param>
    public sealed record Example(string Name, string Description, Func<MainViewModel.DesignDocument> Build)
    {
        /// <summary>
        /// Data it reads that is downloaded when it is opened, rather than bundled; null when there is none.
        /// </summary>
        public ExampleData? Data { get; init; }

        /// <summary>
        /// The field it is from, which its description starts with: "Finance".
        /// </summary>
        public string Category => Description.Split(':', 2) is [string category, _] ? category : string.Empty;

        /// <summary>
        /// What it works out: the first sentence of its description, after the field.
        /// </summary>
        public string Summary
        {
            get
            {
                string text = Description.Split(':', 2) is [_, string rest] ? rest.Trim() : Description;
                // Up to the end of the sentence, or of its first part when a colon goes on to the details.
                int end = new[] { text.IndexOf(". ", StringComparison.Ordinal), text.IndexOf(": ", StringComparison.Ordinal) }
                    .Where(i => i >= 0)
                    .DefaultIfEmpty(-1)
                    .Min();
                text = end < 0 ? text : text[..end] + ".";
                // "the monthly payment" → "The monthly payment"; "π estimated" stays as it is.
                return text.Length > 0 && char.IsAsciiLetterLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
            }
        }
    }

    /// <summary>
    /// Real-world examples, built in code so they always match the current nodes. They run with Julia and
    /// its standard libraries, except that reading .mtx files needs MatrixMarket (offered for installing).
    /// Data they read is bundled in ExampleData, next to the app.
    /// </summary>
    public static partial class Examples
    {
        // Columns of the node centres: inputs, then stages of functions and displays (nodes are 9 cells wide).
        private const int Inputs = 8;
        private const int Stage1 = 22;
        private const int Stage2 = 36;
        private const int Stage3 = 50;

        /// <summary>
        /// The examples, in the order the start page shows them. Made on first use, in a class of its own: the
        /// examples' data is in other parts of this class, whose static members may be set up after this one's.
        /// </summary>
        public static IReadOnlyList<Example> All => Catalogue.All;

        private static class Catalogue
        {
            public static readonly IReadOnlyList<Example> All = [
                new("Loan payment",
                    "Finance: the monthly payment on a 250,000 loan at 4.5% a year over 30 years, and the interest paid in total. "
                    + "Move the sliders in the Parameters panel on the left: it runs again as you change them.",
                    LoanPayment),
                new("Projectile",
                    "Physics: how far and how high a ball thrown at 20 m/s and 45° flies, and for how long (no air resistance). "
                    + "Try other angles with the slider on the left: which one throws furthest?",
                    Projectile),
                new("Line fit",
                    "Statistics: the straight line best fitting test scores against hours studied (least squares), and the score "
                    + "it predicts for 8 hours. Edit the data in the vectors.",
                    LineFit),
                new("PageRank",
                    "Web search: how important four linked web pages are, as Google first ranked them. Row i of the matrix has "
                    + "a 1 for each page that page i links to; the damping is the chance of following a link.",
                    PageRank),
                new("Heat in a rod",
                    "Engineering: the steady temperature along a rod heated evenly and kept cold at both ends, from a sparse "
                    + "matrix of 50 points: the plot shows the temperature along the rod. The hottest point should be close to 1/8, the exact answer.",
                    HeatInARod),
                new("Monte Carlo π",
                    "Simulation: π estimated from 100,000 random points in a square, by counting those inside the quarter circle. "
                    + "The plot shows the points. More samples give a better estimate; run it a few times to see it vary.",
                    MonteCarloPi),
                new("Live Monte Carlo π",
                    "Simulation: π estimated live, from batches of random darts thrown ten times a second. The Every node ticks "
                    + "100 times: at each tick darts throws a new batch (darts a tick, in the Parameters panel), hits keeps the "
                    + "count inside the quarter circle from tick to tick (it takes total as its next value), and estimate is 4 × "
                    + "hits over all the darts so far. A Window keeps the last 100 estimates, and the plot shows them closing in "
                    + "on π; error says how far off it is, about 0.001 after the 2 million darts. A breakpoint, off, waits on the "
                    + "wire into error's display: click its dot and tick On to pause the run once the estimate is within 0.001 of π. "
                    + "Stop ends it early.",
                    LiveMonteCarloPi),
                new("Random walk",
                    "Probability: a random walk, at the press of a button. Press the Button node's Press (or run the design and "
                    + "press it): each press adds 1,000 steps of ±1 (steps a press, in the Parameters panel) to the path, which "
                    + "keeps its value from press to press, and the plot shows the whole walk so far. farthest says how far from "
                    + "the start it has been: after n steps, typically about √n. Stop, and press again to start anew from 0.",
                    RandomWalk),
                new("Weather station week",
                    "Weather: a week of hourly temperatures, one CSV file a day, read one at a time. Map applies daily_mean "
                    + "(which uses mean from Statistics) and daily_swing to every file; the plot shows the mean of each day "
                    + "and warmest says which day was warmest. The files are in ExampleData/daily-temperatures, next to JSynth: "
                    + "point the Files node at your own folder of CSV files.",
                    WeatherStationWeek),
                new("Network spread",
                    "Networks: four small networks (a ring, a star, a path, and four nodes all linked) as sparse matrices in "
                    + ".mtx files. Map counts each network's links and finds its spread rate, the largest eigenvalue "
                    + "(LinearAlgebra), which says how fast a rumour or a virus spreads through it. Reading .mtx files "
                    + "needs the MatrixMarket package: JSynth offers to install it when the example opens.",
                    NetworkSpread),
                new("Savings growth",
                    "Finance: 10,000 saved at 5% a year, year by year for 30 years. Map applies balance to every year of "
                    + "a range, without any files; the plot shows the growth and final the balance after 30 years. "
                    + "Change the rate in balance and run again.",
                    SavingsGrowth),
                new("Radar clutter map",
                    "Radar: scans of a radar spinning on the roof of a car standing at a Toronto junction, from the Boreas "
                    + "dataset (each a PNG image: azimuths by range bins), gone through one at a time. The inputs on the left keep "
                    + "values from scan to scan: after each scan, each takes the value wired into its next port (the ring on its left), "
                    + "as C = update(C, X) in a loop. The clutter map, the scene as it usually is, starts empty and moves a little "
                    + "towards each scan. top_down shows it from above, like a radar screen: the streets of the junction dark, the "
                    + "buildings, trees and parked cars on its corners bright. traffic shows, from above too, in how many scans each "
                    + "spot stood out from the clutter: the tracks of the traffic along the streets. interfered finds the scans where "
                    + "one azimuth stands out along most of its range, another radar's signal, and noted lists them: 2 and 4 of the "
                    + "short set, 25 and 144 of the full one. range_profile gives the clutter's strength range bin by range bin. The "
                    + "scans are downloaded when the example opens, 12 or all 213; reading PNG files needs PNGFiles and ColorTypes.",
                    RadarClutterMap) { Data = BoreasRadar },
                new("Live radar",
                    "Streaming: the radar scans of Radar clutter map, arriving one at a time as from the radar itself. "
                    + "JSynth replays them into a folder, 4 a second as the radar made them, and the Watch folder node "
                    + "works through each as soon as it is written: inputs taking their next values keep the clutter map, count "
                    + "where each scan stood out from it (the traffic) and the scans with another radar's interference, and the "
                    + "plots follow live. Press Stop to end it. The scans are downloaded when the example opens; reading PNG files "
                    + "needs PNGFiles and ColorTypes.",
                    LiveRadar) { Data = BoreasRadar },
                new("Bearing wear",
                    "Machines: the vibration of four bearings on a test rig run until one failed, a week of snapshots from NASA's IMS "
                    + "bearing data, gone through one at a time. Snapshots of the rig stopped are passed over (only when running). "
                    + "strength and spikiness give each bearing's RMS vibration and kurtosis, saved a row a snapshot to bearing-wear.csv "
                    + "next to the data. A Window keeps the last 6 strengths, and smooth averages them: an hour of the full set, so one-off "
                    + "spikes don't set the alarm off. learn takes each bearing's strength when new from the first 12 running snapshots; "
                    + "wear is the most worn bearing's strength over it, plotted as it goes. The first time a bearing gets 10% stronger "
                    + "than when new (1.1, the slider on the left), the alarm notes when and which: bearing 1 on 16 February, 2.5 days before its outer race failed. "
                    + "The snapshots are downloaded when the example opens, every 8th or all 984.",
                    BearingWear) { Data = ImsBearings },
                new("Bearing week in parallel",
                    "Machines: the same week of the four bearings' vibration, every snapshot at once, worked through on all of Julia's "
                    + "threads: features gives each bearing's strength (RMS) and spikiness (kurtosis) in a snapshot, and the Map applies it "
                    + "to every snapshot in parallel (∥ on the Map switches it to one at a time, to compare the time it takes). table "
                    + "holds them a row a snapshot, saved to bearing-features.csv next to the data. wear is each bearing's strength over "
                    + "when new, smoothed over the last 6 snapshots, drawn as a heatmap of the four and a line of the most worn. With "
                    + "the whole week at hand, onset says when each bearing got 10% stronger for good (the slider on the left), which a stream can't know: "
                    + "bearing 1 from early on 16 February, 3 days before its outer race failed, bearings 2 to 4 only in the last day, "
                    + "shaken by it. The snapshots are downloaded when the example opens, every 8th or all 984; all 984 take seconds "
                    + "on a few cores.",
                    BearingWeekInParallel) { Data = ImsBearings },
                new("Earthquake detector",
                    "Seismology: a seismometer's recording, a file a minute, gone through as a station's data logger writes them: "
                    + "station PFO in California on 4 July 2019, as the M6.4 Ridgecrest earthquake struck 200 km away. energy gives "
                    + "each second's energy; a Window keeps the last 5 minutes' typical level, the background. ratio says how much "
                    + "each second shook over it; 4 times (the slider on the left) is a quake, and a new one (the minute before had none) is noted with when and how "
                    + "strong, and saved a row each to quakes.csv next to the data. In the hour from 16:40 UTC it notes the M4.0 foreshock "
                    + "(17:03, 247 times the background), an M2.5 (17:10) and an M1.5 100 km away (17:27) as the USGS catalogue lists "
                    + "them, a 4 times at 17:32 it doesn't, and the M6.4 (17:34); the 3 hours add 4 of its aftershocks. The plots show each minute's seismogram and the "
                    + "loudest second of every minute. The background is a group: double-click it to see its nodes. A breakpoint, off, waits on "
                    + "the wire into only when: click its dot and tick On to pause the run at each new quake. The files are downloaded "
                    + "when the example opens, the hour or 3 hours.",
                    EarthquakeDetector) { Data = RidgecrestSeismograms },
                new("Earthquake network",
                    "Seismology: four seismometers in southern California as the M6.4 Ridgecrest earthquake struck, their minutes "
                    + "gone through in step: a Zip node takes each station's minute of the same name together. "
                    + "One station gets fooled by traffic or wind; a network only believes a quake two stations or more hear within "
                    + "a minute of each other. Each station's seconds are compared with its background over the 5 minutes before, 4 times it (the slider on the left) a quake; "
                    + "heard gives when each heard a new quake, and confirmed the quakes the network believes, noted with when and "
                    + "where, and saved a row each to quakes.csv next to the data. In the hour from 16:40 UTC it notes the M4.0 "
                    + "foreshock (17:03), an M2.5 (17:09), an M2.1 (17:12), an M1.5 (17:27) and the M6.4 (17:33), as the USGS "
                    + "catalogue lists them, and nothing else: what only one station felt is left out. The 3 hours add 13 of its aftershocks, and nothing in the quiet hour before. The files "
                    + "are downloaded when the example opens, the hour or 3 hours of each station.",
                    EarthquakeNetwork) { Data = RidgecrestNetwork },
                new("Motion in video",
                    "Video: what moves in the frames of a fixed camera over a highway, from the CDnet 2014 change detection "
                    + "dataset, gone through one at a time. As with the radar's clutter map, the background is the scene as it "
                    + "usually is; moving marks the pixels far from it (further than the slider on the left), and update makes it follow the scene, only where nothing "
                    + "moves. Each frame's mask is saved as a picture (masks/mask-in000123.png next to the data) and scored against "
                    + "the frame's twin marked by hand, found with the file path: f_score gives the precision, recall and F (1 is "
                    + "perfect) of every frame so far: 0.82 on the first 800 frames, 0.84 on all. The frames are downloaded when the example opens, 800 or all "
                    + "1,700; reading PNG files needs PNGFiles and ColorTypes.",
                    MotionInVideo) { Data = CdnetHighway },
                new("Live air traffic",
                    "Streaming: the aircraft over the Netherlands, Belgium and western Germany right now, from the OpenSky "
                    + "Network's live web service. The Watch folder node fetches its address every 15 seconds and saves each answer "
                    + "as a JSON file, named by the time; latest only means a slow run skips to the newest answer rather than falling "
                    + "behind. airborne turns an answer into a row an aircraft (position, altitude, climb rate, speed), plotted as a "
                    + "map; their count is saved a row a fetch to traffic.csv and plotted; steep lists those climbing or descending faster than the slider on the left. "
                    + "Press Stop to end it. Without an account OpenSky answers 400 times a day: at one fetch every 15 seconds, about "
                    + "an hour and 40 minutes. OpenSky's data is for research and other non-commercial use "
                    + "(https://opensky-network.org); reading JSON needs the JSON package.",
                    LiveAirTraffic),
                new("Storm surge",
                    "Oceanography: Hurricane Helene's storm surge along Florida's Gulf coast in September 2024, from six tide gauges "
                    + "and the storm's track, merged into one stream: a Merge node goes through every gauge's hourly files and the "
                    + "storm's positions together, in time order, and its folder output says where each came from; only "
                    + "when it is a gauge's hour, surge is its highest water level over the tide predicted for that minute. Each "
                    + "folder's own value is its latest file, so row takes the storm's latest position for every gauge hour: how far "
                    + "the storm was, and on which side. highest keeps each gauge's highest surge, saved a row an hour to surge.csv "
                    + "next to the data, and the heatmap shows the surge moving up the coast. The gauges with the storm passing to "
                    + "their west, its winds blowing onshore, rose most: Cedar Key 3.15 m, a record, St. Petersburg 2.06 m with the "
                    + "storm 270 km away; Apalachicola, to its west, 1.31 m though it passed closer. The files are downloaded when "
                    + "the example opens.",
                    HeleneSurgeMerged) { Data = HeleneSurge },
            ];
        }


        /// <summary>
        /// An empty vector or matrix of <paramref name="elementType"/>: an input starting with nothing, which its
        /// next values fill.
        /// </summary>
        private static ArrayBuilder Empty(string shape, string elementType, int row)
        {
            ArrayBuilder empty = new() { Kind = "zeros", Shape = shape, ElementType = elementType, Row = row, Column = Inputs };
            foreach (Argument size in empty.Parameters.OfType<Argument>().Where(a => a.Key is "m" or "n"))
            {
                size.Value = "0";
            }

            return empty;
        }

        /// <summary>
        /// A vector of <paramref name="n"/> zeros: an input starting at nothing for each of a network's stations.
        /// </summary>
        private static ArrayBuilder Zeros(int n, int row)
        {
            ArrayBuilder zeros = Empty(JuliaTypes.Vector, "Float64", row);
            foreach (Argument size in zeros.Parameters.OfType<Argument>().Where(a => a.Key == "n"))
            {
                size.Value = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return zeros;
        }

        /// <summary>
        /// A folder of bundled example data, next to the app (see ExampleData in the project).
        /// </summary>
        private static string DataFolder(string name)
        {
            return System.IO.Path.Combine(AppContext.BaseDirectory, "ExampleData", name);
        }

        private static Float Scalar(double value, int row)
        {
            // The bitness first: setting it resets the range.
            return new Float { SelectedType = "64-bit", Value = value, Row = row, Column = Inputs };
        }

        /// <summary>
        /// A Display node, to see a result on the canvas after a run.
        /// </summary>
        private static DisplayNode Shown(int row, int column)
        {
            return new DisplayNode { Row = row, Column = column };
        }

        /// <summary>
        /// <paramref name="node"/> put in the parameters panel: named <paramref name="name"/> (null: after the input it
        /// is wired into), the number kept from <paramref name="from"/> to <paramref name="to"/>, its slider's range.
        /// </summary>
        private static T InPanel<T>(T node, string? name, double from, double to) where T : Node
        {
            (node.InPanel, node.PanelName) = (true, name ?? string.Empty);
            switch (node)
            {
                case Float x:
                    (x.Min, x.Max) = (from, to);
                    break;
                case Integer z:
                    (z.Min, z.Max) = ((long)from, (long)to);
                    break;
                case UnsignedInteger n:
                    (n.Min, n.Max) = ((UInt128)from, (UInt128)to);
                    break;
            }

            return node;
        }

        private static Integer Whole(long value, int row)
        {
            return new Integer { SelectedType = "64-bit", Value = value, Row = row, Column = Inputs };
        }

        private static Function Define(string name, string returnType, int row, int column, string body, params (string Name, string Type)[] parameters)
        {
            Function function = new() { Name = name, ReturnType = returnType, Body = body, Row = row, Column = column };
            foreach ((string parameterName, string type) in parameters)
            {
                function.Parameters.Add(new Parameter(parameterName, type));
            }

            return function;
        }

        /// <summary>
        /// The nodes with their wires, each from a node's value (or another of its outputs, given as the node and the
        /// output's name) to an input given by position or by name; a wire the wiring rules refuse is a mistake in the
        /// example.
        /// </summary>
        private static MainViewModel.DesignDocument Design(Node[] nodes, params (object Source, Node Target, object Input)[] wires)
        {
            MainViewModel.DesignDocument design = new();
            design.Nodes.AddRange(nodes);
            foreach ((object from, Node target, object input) in wires)
            {
                (Node source, string? output) = from is (Node node, string name) ? (node, name) : ((Node)from, null);
                Parameter parameter = input is int index ? target.Inputs[index] : input is "next" ? target.Next! : target.Inputs.Single(p => p.Name == (string)input);
                if (Wiring.Validate(design.Nodes, design.Connections, source, target, parameter, output) is string error)
                {
                    throw new InvalidOperationException($"Example wire to {parameter.Name}: {error}");
                }

                design.Connections.Add(new Connection(source.ID, target.ID, parameter.ID, output));
                target.Wired(design.Connections);
            }

            Layout.Arrange(design.Nodes, Held(design));
            return design;
        }

        /// <summary>
        /// Puts each node at its cell: a frame's nodes in a band of rows of their own, so that the frames come one after
        /// the other.
        /// </summary>
        private static void Place(params (Node Node, int Row, int Column)[] places)
        {
            foreach ((Node node, int row, int column) in places)
            {
                (node.Row, node.Column) = (row, column);
            }
        }

        // The nodes each example's frame is drawn around (see Framed).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameNode, Node[]> FramedNodes = [];

        /// <summary>
        /// A frame titled <paramref name="title"/>, its note <paramref name="note"/>, drawn around <paramref name="nodes"/>
        /// when the design is arranged.
        /// </summary>
        private static FrameNode Framed(string title, string note, params Node[] nodes)
        {
            FrameNode frame = new() { Title = title, Note = note, Row = nodes.Min(n => n.Row), Column = nodes.Min(n => n.Column) };
            FramedNodes.Add(frame, nodes);
            return frame;
        }

        /// <summary>
        /// The nodes each frame of <paramref name="design"/> holds: those it was made around, or the group made of them.
        /// </summary>
        private static Dictionary<FrameNode, Node[]> Held(MainViewModel.DesignDocument design) =>
            design.Nodes.OfType<FrameNode>().ToDictionary(f => f, f => FramedNodes.TryGetValue(f, out Node[]? nodes)
                ? design.Nodes.Where(n => nodes.Contains(n) || (n is GroupNode g && g.Members.Intersect(nodes).Any())).ToArray()
                : design.Nodes.Where(n => n is not FrameNode && f.Holds(n)).ToArray());

        /// <summary>
        /// <paramref name="design"/> with <paramref name="nodes"/> made one group, named <paramref name="name"/>, and
        /// arranged again.
        /// </summary>
        private static GroupNode Grouped(MainViewModel.DesignDocument design, string name, params Node[] nodes)
        {
            GroupNode group = MainViewModel.Group(design.Nodes, design.Connections, [.. nodes], name);
            Layout.Arrange(design.Nodes, Held(design));
            return group;
        }

        /// <summary>
        /// A breakpoint on the wire from <paramref name="source"/> to <paramref name="target"/>, pausing when
        /// <paramref name="condition"/> holds; off, for the example's reader to switch on.
        /// </summary>
        private static void PauseOn(MainViewModel.DesignDocument design, Node source, Node target, string condition)
        {
            design.Connections.Single(c => c.SourceID == source.ID && c.TargetID == target.ID).Breakpoint = new Breakpoint(false, condition);
        }
    }
}
