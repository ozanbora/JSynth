using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// A ready-made design showing what JuliaSynth is for.
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
        public string Category => Description.Split(':', 2) is [string field, _] ? field : string.Empty;

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
    public static class Examples
    {
        // Columns of the node centres: inputs, then stages of functions and displays (nodes are 9 cells wide).
        private const int Inputs = 8;
        private const int Stage1 = 22;
        private const int Stage2 = 36;
        private const int Stage3 = 50;

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

        public static IReadOnlyList<Example> All { get; } = [
            new("Loan payment",
                "Finance: the monthly payment on a 250,000 loan at 4.5% a year over 30 years, and the interest paid in total. "
                + "Change the amount, the rate or the years and run again (F5).",
                LoanPayment),
            new("Projectile",
                "Physics: how far and how high a ball thrown at 20 m/s and 45° flies, and for how long (no air resistance). "
                + "Try other angles: which one throws furthest?",
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
            new("Weather station week",
                "Weather: a week of hourly temperatures, one CSV file a day, read one at a time. Map applies daily_mean "
                + "(which uses mean from Statistics) and daily_swing to every file; the plot shows the mean of each day "
                + "and warmest says which day was warmest. The files are in ExampleData/daily-temperatures, next to JuliaSynth: "
                + "point the Files node at your own folder of CSV files.",
                WeatherStationWeek),
            new("Network spread",
                "Networks: four small networks (a ring, a star, a path, and four nodes all linked) as sparse matrices in "
                + ".mtx files. Map counts each network's links and finds its spread rate, the largest eigenvalue "
                + "(LinearAlgebra), which says how fast a rumour or a virus spreads through it. Reading .mtx files "
                + "needs the MatrixMarket package: JuliaSynth offers to install it when the example opens.",
                NetworkSpread),
            new("Savings growth",
                "Finance: 10,000 saved at 5% a year, year by year for 30 years. Map applies balance to every year of "
                + "a range, without any files; the plot shows the growth and final the balance after 30 years. "
                + "Change the rate in balance and run again.",
                SavingsGrowth),
            new("Radar clutter map",
                "Radar: scans of a radar spinning on the roof of a car standing at a Toronto junction, from the Boreas "
                + "dataset (each a PNG image: azimuths by range bins). Running keeps a clutter map, the scene as it usually is, "
                + "as a running average of the scans. top_down shows it from above, like a radar screen: the streets of the junction "
                + "dark, the buildings, trees and parked cars on its corners bright. standing_out shows, from above too, in how many scans each "
                + "spot stood out from the clutter: the tracks of the traffic along the streets. interference finds the scans where one "
                + "azimuth stands out along most of its range, another radar's signal: 2 and 4 of the short set, 3 and 25 of the full one. "
                + "range_profile gives the clutter's strength range bin by range bin. The scans are "
                + "downloaded when the example opens, 12 or all 213; reading PNG files needs PNGFiles and ColorTypes.",
                RadarClutterMap) { Data = BoreasRadar },
        ];


        private static MainViewModel.DesignDocument LoanPayment()
        {
            Float amount = Scalar(250_000, 5);
            Float rate = Scalar(0.045, 13);
            Integer years = Whole(30, 21);

            Function payment = Define("payment", "Float64", 9, Stage1,
                """
                i = rate / 12        # monthly interest rate
                m = 12 * years       # number of payments
                P * i / (1 - (1 + i)^(-m))
                """,
                ("P", "Float64"), ("rate", "Float64"), ("years", "Int64"));

            Function interest = Define("interest", "Float64", 19, Stage2,
                "monthly * 12 * years - P",
                ("monthly", "Float64"), ("P", "Float64"), ("years", "Int64"));

            DisplayNode showPayment = Shown(6, Stage2);
            DisplayNode showInterest = Shown(19, Stage3);

            return Design([amount, rate, years, payment, interest, showPayment, showInterest],
                (payment, showPayment, 0), (interest, showInterest, 0),
                (amount, payment, 0), (rate, payment, 1), (years, payment, 2),
                (payment, interest, 0), (amount, interest, 1), (years, interest, 2));
        }

        private static MainViewModel.DesignDocument Projectile()
        {
            Float speed = Scalar(20, 9);
            Float angle = Scalar(45, 19);

            Function distance = Define("distance", "Float64", 4, Stage1, "v^2 * sind(2angle) / 9.81 # metres", ("v", "Float64"), ("angle", "Float64"));
            Function peak = Define("peak", "Float64", 14, Stage1, "(v * sind(angle))^2 / (2 * 9.81) # metres", ("v", "Float64"), ("angle", "Float64"));
            Function airtime = Define("airtime", "Float64", 24, Stage1, "2v * sind(angle) / 9.81 # seconds", ("v", "Float64"), ("angle", "Float64"));

            DisplayNode[] shown = [Shown(4, Stage2), Shown(14, Stage2), Shown(24, Stage2)];

            return Design([speed, angle, distance, peak, airtime, .. shown],
                (distance, shown[0], 0), (peak, shown[1], 0), (airtime, shown[2], 0),
                (speed, distance, 0), (angle, distance, 1),
                (speed, peak, 0), (angle, peak, 1),
                (speed, airtime, 0), (angle, airtime, 1));
        }

        private static MainViewModel.DesignDocument LineFit()
        {
            VectorNode hours = new() { Text = "[1, 2, 3, 4, 5, 6]" };
            VectorNode scores = new() { Text = "[52, 55, 61, 64, 70, 73]" };
            Float question = Scalar(8, 21);
            (hours.Row, hours.Column) = (5, Inputs);
            (scores.Row, scores.Column) = (13, Inputs);

            Function fit = Define("fit", "Vector{Float64}", 9, Stage1,
                """
                # Solve [x 1] * [slope, intercept] ≈ y in the least-squares sense.
                [x ones(length(x))] \ y
                """,
                ("x", "Vector{Float64}"), ("y", "Vector{Float64}"));

            Function predict = Define("predict", "Float64", 19, Stage2,
                "line[1] * hours + line[2]",
                ("line", "Vector{Float64}"), ("hours", "Float64"));

            DisplayNode showLine = Shown(6, Stage2);
            DisplayNode showScore = Shown(19, Stage3);

            return Design([hours, scores, question, fit, predict, showLine, showScore],
                (fit, showLine, 0), (predict, showScore, 0),
                (hours, fit, 0), (scores, fit, 1), (fit, predict, 0), (question, predict, 1));
        }

        private static MainViewModel.DesignDocument PageRank()
        {
            MatrixNode links = new() { Text = "[0 1 1 0; 0 0 1 0; 1 0 0 1; 0 0 1 0]", Row = 5, Column = Inputs };
            Float damping = Scalar(0.85, 13);

            Function pagerank = Define("pagerank", "Vector{Float64}", 9, Stage1,
                """
                n = size(links, 1)
                # Column j spreads page j's rank evenly over the pages it links to.
                M = links' ./ max.(sum(links, dims = 2)', 1)
                r = fill(1 / n, n)
                for _ in 1:100
                    r = (1 - d) / n .+ d .* (M * r)
                end
                r
                """,
                ("links", "Matrix{Float64}"), ("d", "Float64"));

            Function top = Define("top", "Int64", 19, Stage2, "argmax(rank) # the most important page", ("rank", "Vector{Float64}"));

            DisplayNode showRanks = Shown(6, Stage2);
            DisplayNode showTop = Shown(19, Stage3);

            return Design([links, damping, pagerank, top, showRanks, showTop],
                (pagerank, showRanks, 0), (top, showTop, 0),
                (links, pagerank, 0), (damping, pagerank, 1), (pagerank, top, 0));
        }

        private static MainViewModel.DesignDocument HeatInARod()
        {
            Integer points = Whole(50, 5);
            Float length = Scalar(1, 21);

            ArrayBuilder heat = new() { Kind = "fill", Row = 13, Column = Stage1 };

            Function laplacian = Define("laplacian", "SparseMatrixCSC{Float64, Int64}", 5, Stage1,
                """
                # Second differences between neighbouring points: 2 on the diagonal, -1 beside it.
                spdiagm(-1 => fill(-1.0, n - 1), 0 => fill(2.0, n), 1 => fill(-1.0, n - 1))
                """,
                ("n", "Int64"));

            Function temperature = Define("temperature", "Vector{Float64}", 9, Stage2,
                """
                h = L / (size(A, 1) + 1)   # distance between points
                h^2 * (A \ q)
                """,
                ("A", "AbstractMatrix{Float64}"), ("q", "Vector{Float64}"), ("L", "Float64"));

            Function hottest = Define("hottest", "Float64", 9, Stage3, "maximum(T)", ("T", "Vector{Float64}"));

            DisplayNode showHottest = Shown(9, Stage3 + 14);
            PlotNode curve = new() { Title = "Temperature along the rod", Row = 24, Column = Stage3 };

            return Design([points, length, heat, laplacian, temperature, hottest, showHottest, curve],
                (temperature, curve, "y"),
                (hottest, showHottest, 0),
                (points, laplacian, 0), (points, heat, "n"),
                (laplacian, temperature, 0), (heat, temperature, 1), (length, temperature, 2),
                (temperature, hottest, 0));
        }

        private static MainViewModel.DesignDocument MonteCarloPi()
        {
            Integer samples = Whole(100_000, 9);
            ArrayBuilder x = new() { Kind = "rand", Row = 5, Column = Stage1 };
            ArrayBuilder y = new() { Kind = "rand", Row = 13, Column = Stage1 };

            Function estimate = Define("estimate", "Float64", 9, Stage2,
                """
                # The share of points inside the quarter circle is π/4.
                4 * count(x .^ 2 .+ y .^ 2 .<= 1) / length(x)
                """,
                ("x", "Vector{Float64}"), ("y", "Vector{Float64}"));

            DisplayNode showEstimate = Shown(9, Stage3);
            PlotNode cloud = new() { Kind = "Scatter", Title = "Random points", Row = 24, Column = Stage2 };

            return Design([samples, x, y, estimate, showEstimate, cloud],
                (y, cloud, "y"), (x, cloud, "x"),
                (estimate, showEstimate, 0),
                (samples, x, "n"), (samples, y, "n"), (x, estimate, 0), (y, estimate, 1));
        }

        private static MainViewModel.DesignDocument WeatherStationWeek()
        {
            FilesNode days = new() { Folder = DataFolder("daily-temperatures"), Pattern = "day-*.csv", HasHeader = true, Row = 6, Column = Inputs };

            Function dailyMean = Define("daily_mean", "Float64", 16, Inputs, "mean(A[:, 2])  # column 2: °C", ("A", "Matrix{Float64}"));
            dailyMean.Packages = "Statistics";
            Function dailySwing = Define("daily_swing", "Float64", 26, Inputs,
                "maximum(A[:, 2]) - minimum(A[:, 2])  # warmest minus coldest hour",
                ("A", "Matrix{Float64}"));

            MapNode means = new() { Row = 11, Column = Stage1 };
            MapNode swings = new() { Row = 25, Column = Stage1 };
            PlotNode curve = new() { Title = "Mean temperature per day (°C)", Row = 9, Column = Stage2 + 3 };
            Function warmest = Define("warmest", "Int64", 22, Stage2, "argmax(means)  # the day of the week", ("means", "Vector{Float64}"));
            DisplayNode showWarmest = Shown(22, Stage3);
            DisplayNode showSwings = Shown(34, Stage2);

            return Design([days, dailyMean, dailySwing, means, swings, curve, warmest, showWarmest, showSwings],
                (days, means, "items"), (dailyMean, means, "f"),
                (days, swings, "items"), (dailySwing, swings, "f"),
                (means, curve, "y"), (means, warmest, 0), (warmest, showWarmest, 0),
                (swings, showSwings, 0));
        }

        private static MainViewModel.DesignDocument NetworkSpread()
        {
            FilesNode networks = new() { Format = FileFormats.MatrixMarket, Folder = DataFolder("networks"), Row = 6, Column = Inputs };

            Function links = Define("links", "Int64", 16, Inputs, "nnz(S) ÷ 2  # each link is stored both ways", ("S", "SparseMatrixCSC{Float64, Int64}"));
            Function spread = Define("spread", "Float64", 26, Inputs,
                """
                # The largest eigenvalue of the links: the faster things spread, the larger it is.
                maximum(abs, eigvals(Matrix(S)))
                """,
                ("S", "SparseMatrixCSC{Float64, Int64}"));
            spread.Packages = "LinearAlgebra";

            MapNode linkCounts = new() { Row = 11, Column = Stage1 };
            MapNode spreadRates = new() { Row = 25, Column = Stage1 };
            DisplayNode showLinks = Shown(11, Stage2);
            Function fastest = Define("fastest", "Int64", 25, Stage2, "argmax(rates)  # in file order: complete-4, path-5, ring-6, star-6", ("rates", "Vector{Float64}"));
            DisplayNode showFastest = Shown(25, Stage3);
            DisplayNode showRates = Shown(35, Stage2);

            return Design([networks, links, spread, linkCounts, spreadRates, showLinks, fastest, showFastest, showRates],
                (networks, linkCounts, "items"), (links, linkCounts, "f"),
                (networks, spreadRates, "items"), (spread, spreadRates, "f"),
                (linkCounts, showLinks, 0), (spreadRates, fastest, 0), (fastest, showFastest, 0),
                (spreadRates, showRates, 0));
        }

        private static MainViewModel.DesignDocument SavingsGrowth()
        {
            ArrayBuilder years = new() { Kind = "range", Row = 8, Column = Inputs };
            foreach ((string key, string value) in new[] { ("start", "0"), ("stop", "30"), ("n", "31") })
            {
                years.Parameters.OfType<Argument>().Single(a => a.Key == key).Value = value;
            }

            Function balance = Define("balance", "Float64", 18, Inputs, "10_000 * 1.05^t  # 10,000 saved at 5% a year, after t years", ("t", "Float64"));
            MapNode growth = new() { Row = 12, Column = Stage1 };
            PlotNode curve = new() { Title = "Balance by year", Row = 10, Column = Stage2 + 3 };
            Function final = Define("final", "Float64", 24, Stage2, "last(balances)", ("balances", "Vector{Float64}"));
            DisplayNode showFinal = Shown(24, Stage3);

            return Design([years, balance, growth, curve, final, showFinal],
                (years, growth, "items"), (balance, growth, "f"),
                (years, curve, "x"), (growth, curve, "y"), (growth, final, 0), (final, showFinal, 0));
        }

        /// <summary>
        /// A folder of bundled example data, next to the app (see ExampleData in the project).
        /// </summary>
        private static MainViewModel.DesignDocument RadarClutterMap()
        {
            // The full set when it was downloaded, else the short one.
            string folder = BoreasRadar.DownloadedFolder ?? ExampleData.SetFolder(BoreasRadar.Folder, BoreasRadar.Sets[0]);
            FilesNode files = new() { Format = FileFormats.Png, Folder = folder, Row = 6, Column = Inputs };

            Function scan = Define("scan", "Matrix{Float64}", 16, Inputs,
                "X[:, 12:1011]  # without the 11 timestamp columns; the first 1000 range bins (about 60 m)",
                ("X", "Matrix{Float64}"));
            Function update = Define("update", "Matrix{Float64}", 26, Inputs,
                """
                α = 0.2  # how quickly the clutter map follows the scene
                (1 - α) .* C .+ α .* X
                """,
                ("C", "Matrix{Float64}"), ("X", "Matrix{Float64}"));

            MapNode scans = new() { Row = 11, Column = Stage1 };
            RunningNode clutter = new() { Row = 25, Column = Stage1 };

            Function standingOut = Define("standing_out", "Matrix{Float64}", 9, Stage2,
                """
                # In how many scans each spot around the car stood out from the clutter, seen from above with
                # top_down, 3 or more brightest: what moves leaves tracks, traffic along the streets.
                times = sum(X -> (X .- C) .> 0.15, scans)
                top_down(Float64.(min.(times, 3)))
                """,
                ("scans", "Vector"), ("C", "Matrix{Float64}"));
            Function topDown = Define("top_down", "Matrix{Float64}", 29, Stage2,
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
                """,
                ("C", "Matrix{Float64}"));
            Function rangeProfile = Define("range_profile", "Vector{Float64}", 19, Stage2,
                "vec(sum(C; dims = 1)) ./ size(C, 1)  # the clutter's mean strength at each range bin",
                ("C", "Matrix{Float64}"));
            Function interference = Define("interference", "Vector{Int64}", 39, Stage2,
                """
                # Scans where an azimuth stands out from the clutter along most of its range, in 8 of its
                # 10 stretches of 100 bins (6 m each): another radar's signal. A vehicle covers a few metres.
                lit(X, a) = count(b -> count(>(0.15), X[a, b:b+99] .- C[a, b:b+99]) >= 10, 1:100:1000)
                findall(X -> any(a -> lit(X, a) >= 8, axes(X, 1)), scans)
                """,
                ("scans", "Vector"), ("C", "Matrix{Float64}"));

            DisplayNode showInterference = Shown(42, Stage3);
            PlotNode clutterMap = new() { Title = "Clutter map from above (0.3 m a pixel)", Kind = "Heatmap", Row = 35, Column = Stage3 + 17 };
            PlotNode traffic = new() { Title = "Standing out from the clutter, from above", Kind = "Heatmap", Row = 7, Column = Stage3 + 17 };
            PlotNode profile = new() { Title = "Clutter strength by range bin", Row = 21, Column = Stage3 + 17 };

            return Design([files, scan, update, scans, clutter, standingOut, topDown, rangeProfile, interference, showInterference, clutterMap, traffic, profile],
                (files, scans, "items"), (scan, scans, "f"),
                (scans, clutter, "items"), (update, clutter, "f"),
                (scans, standingOut, 0), (clutter, standingOut, 1), (scans, interference, 0), (clutter, interference, 1), (interference, showInterference, 0),
                (clutter, rangeProfile, 0), (clutter, topDown, 0), (topDown, clutterMap, "y"), (standingOut, traffic, "y"), (rangeProfile, profile, "y"));
        }

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
        /// The nodes with their wires, each to an input given by position or by name; a wire the wiring
        /// rules refuse is a mistake in the example.
        /// </summary>
        private static MainViewModel.DesignDocument Design(Node[] nodes, params (Node Source, Node Target, object Input)[] wires)
        {
            MainViewModel.DesignDocument design = new();
            design.Nodes.AddRange(nodes);
            foreach ((Node source, Node target, object input) in wires)
            {
                Parameter parameter = input is int index ? target.Inputs[index] : target.Inputs.Single(p => p.Name == (string)input);
                if (Wiring.Validate(design.Connections, source, target, parameter) is string error)
                {
                    throw new InvalidOperationException($"Example wire to {parameter.Name}: {error}");
                }

                design.Connections.Add(new Connection(source.ID, target.ID, parameter.ID));
            }

            return design;
        }
    }
}
