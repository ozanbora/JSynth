namespace JuliaSynth.Model
{
    /// <summary>
    /// A button on the canvas: a design with one runs as a stream (see <see cref="TickNode"/>), the nodes after it again
    /// each time it is pressed, until stopped. Pressed while nothing runs, it runs the design, that press the first.
    /// </summary>
    public class ButtonNode : TickNode
    {
        /// <summary>
        /// Julia code waiting for presses, for runs: JuliaSynth writes a line to a file for each.
        /// </summary>
        public static string Prelude { get; } = """
            # Presses of a button, made by a JuliaSynth Button node: body(t, n, true) for each, t the seconds since the start
            # and n the press's number, as JuliaSynth writes a line for each to the file `presses`, until the stop file
            # appears. Presses coming while the nodes after it are still worked out wait their turn. ready() once it waits.
            function _jsynth_presses(body, presses; stop = nothing, ready = () -> nothing)
                stopped() = stop !== nothing && isfile(stop)
                ready()
                start, n = time(), 0
                while !stopped()
                    if (isfile(presses) ? countlines(presses) : 0) > n
                        n += 1
                        body(time() - start, n, true)
                    else
                        sleep(0.05)
                    end
                end
                n
            end
            """.ReplaceLineEndings("\n");

        public ButtonNode()
        {
            Color = NodePalette.Button;
        }

        public override string Label => "at each press";

        public override string TickName => "press";

        public override string PreludeCode => Prelude;

        /// <summary>
        /// The file a run's presses are written to, a line each: next to its stop file.
        /// </summary>
        public static string PressesFile(string stopFile) => stopFile + ".presses";

        /// <summary>
        /// In a run, waiting for presses; in an exported script, with no button to press, pressed once.
        /// </summary>
        public override string Start(string? stopFile) => stopFile is null
            ? "foreach(((0.0, 1, true),)) do (_path, _n, _show)"
            : $"_jsynth_presses({ArrayFile.PathLiteral(PressesFile(stopFile))}; stop = {ArrayFile.PathLiteral(stopFile)}, ready = {Ready}) do _path, _n, _show";
    }
}
