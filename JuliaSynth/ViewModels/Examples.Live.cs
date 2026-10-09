using JuliaSynth.Model;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// Examples that run as a stream without files: ticking with a clock, or at the press of a button.
    /// </summary>
    public static partial class Examples
    {
        private static MainViewModel.DesignDocument LiveMonteCarloPi()
        {
            EveryNode clock = new() { Seconds = 0.1, Limit = 100, Row = 5, Column = Inputs };
            Integer batch = InPanel(Whole(20_000, 13), "darts a tick", 1_000, 200_000, 1, 10_000_000);
            Integer hits = Whole(0, 21);

            Function darts = Define("darts", "Int64", 5, Stage1,
                """
                # A new batch of n darts at each tick: how many land inside the quarter circle.
                count(_ -> rand()^2 + rand()^2 <= 1, 1:n)
                """,
                ("n", "Int64"), ("tick", "Int64"));
            Function total = Define("total", "Int64", 17, Stage1, "hits + new  # inside, over all the ticks so far", ("hits", "Int64"), ("new", "Int64"));
            Function estimate = Define("estimate", "Float64", 9, Stage2,
                "4 * hits / (n * tick)  # the share inside is π/4",
                ("hits", "Int64"), ("n", "Int64"), ("tick", "Int64"));
            Function error = Define("error", "Float64", 21, Stage3, "abs(estimate - π)", ("estimate", "Float64"));

            WindowNode history = new() { Size = 100, Row = 21, Column = Stage2 };
            PlotNode plot = new() { Title = "π estimated, tick by tick", Row = 30, Column = Stage3 };
            DisplayNode showEstimate = Shown(9, Stage3);
            DisplayNode showError = Shown(21, Stage3 + 14);

            FrameNode carried = Framed("Kept from tick to tick", "hits takes total as its next value: the darts inside over all the ticks so far.",
                hits, total);
            FrameNode howClose = Framed("How close",
                "The wire into error's display has a breakpoint, off: click its dot and tick On to pause once the estimate is "
                + "within 0.001 of π.",
                error, showError);

            MainViewModel.DesignDocument design = Design([clock, batch, carried, howClose, hits, darts, total, estimate, error, history, plot, showEstimate, showError],
                (clock, darts, "tick"), (batch, darts, "n"),
                (hits, total, "hits"), (darts, total, "new"), (total, hits, "next"),
                (total, estimate, "hits"), (batch, estimate, "n"), (clock, estimate, "tick"),
                (estimate, showEstimate, 0), (estimate, error, 0), (error, showError, 0),
                (estimate, history, "value"), (history, plot, "y"));
            PauseOn(design, error, showError, "x < 0.001");
            return design;
        }

        private static MainViewModel.DesignDocument RandomWalk()
        {
            ButtonNode press = new() { Row = 5, Column = Inputs };
            Integer steps = InPanel(Whole(1_000, 13), "steps a press", 10, 10_000, 1, 1_000_000);
            ArrayBuilder path = Empty(JuliaTypes.Vector, "Float64", 21);

            Function walk = Define("walk", "Vector{Float64}", 15, Stage1,
                """
                # n more steps of ±1, on from where the walk is.
                start = isempty(path) ? 0.0 : path[end]
                vcat(path, start .+ cumsum(rand((-1.0, 1.0), n)))
                """,
                ("path", "Vector{Float64}"), ("n", "Int64"));
            Function farthest = Define("farthest", "Float64", 15, Stage2, "maximum(abs, path)  # from the start, so far", ("path", "Vector{Float64}"));
            Function position = Define("position", "String", 5, Stage2,
                "string(\"after \", presses, presses == 1 ? \" press: \" : \" presses: \", length(path), \" steps, at \", path[end])",
                ("path", "Vector{Float64}"), ("presses", "Int64"));

            PlotNode plot = new() { Title = "The walk so far", Row = 30, Column = Stage2 };
            DisplayNode showPosition = Shown(5, Stage3);
            DisplayNode showFarthest = Shown(15, Stage3);

            return Design([press, steps, path, walk, farthest, position, plot, showPosition, showFarthest],
                (path, walk, "path"), (steps, walk, "n"), (walk, path, "next"),
                (walk, plot, "y"), (walk, farthest, 0), (farthest, showFarthest, 0),
                (walk, position, "path"), (press, position, "presses"), (position, showPosition, 0));
        }
    }
}
