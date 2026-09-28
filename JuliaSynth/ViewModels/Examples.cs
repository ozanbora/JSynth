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
    /// Real-world examples, built in code so they always match the current nodes. Each runs with plain Julia:
    /// no packages to install.
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
