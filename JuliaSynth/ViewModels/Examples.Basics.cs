using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// The first examples: everyday numbers, physics and graphs, worked out once.
    /// </summary>
    public static partial class Examples
    {
        private static MainViewModel.DesignDocument LoanPayment()
        {
            Float amount = InPanel(Scalar(250_000, 5), "loan", 10_000, 1_000_000, 0, 1e9);
            Float rate = InPanel(Scalar(0.045, 13), "rate", 0, 0.15, 0, 1);
            Integer years = InPanel(Whole(30, 21), "years", 1, 40, 1, 100);

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
            Float speed = InPanel(Scalar(20, 9), "speed", 0, 50, 0, 1_000);
            Float angle = InPanel(Scalar(45, 19), "angle", 0, 90, 0, 90);

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
            VectorNode hours = new() { Text = "[1, 2, 3, 4, 5, 6]", PanelName = "hours" };
            VectorNode scores = new() { Text = "[52, 55, 61, 64, 70, 73]", PanelName = "scores" };
            Float question = InPanel(Scalar(8, 21), "new hours", 0, 12, 0, 24);
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
            MatrixNode links = new() { Text = "[0 1 1 0; 0 0 1 0; 1 0 0 1; 0 0 1 0]", PanelName = "links", Row = 5, Column = Inputs };
            Float damping = InPanel(Scalar(0.85, 13), "damping", 0, 1, 0, 1);

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
            Integer points = InPanel(Whole(50, 5), "points", 5, 200, 2, 10_000);
            Float length = InPanel(Scalar(1, 21), "length", 0.1, 5, 0.001, 1_000);

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
            Integer samples = InPanel(Whole(100_000, 9), "samples", 1_000, 1_000_000, 1, 100_000_000);
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
    }
}
