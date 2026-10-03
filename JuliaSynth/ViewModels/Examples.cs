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
    public sealed record Example(string Name, string Description, Func<MainViewModel.DesignDocument> Build);

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
                + "needs the MatrixMarket package: JuliaSynth offers to install it on the first run.",
                NetworkSpread),
            new("Savings growth",
                "Finance: 10,000 saved at 5% a year, year by year for 30 years. Map applies balance to every year of "
                + "a range, without any files; the plot shows the growth and final the balance after 30 years. "
                + "Change the rate in balance and run again.",
                SavingsGrowth),
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
