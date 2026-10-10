namespace JSynth.Model
{
    /// <summary>
    /// A node leading a stream by ticks rather than files: a design with one runs the nodes after it again at each
    /// tick, until stopped. Its value is the tick's number, from 1; its <see cref="SecondsOutput"/> the seconds since the
    /// start. An input node with a next value wired (see <see cref="Node.Next"/>) carries a state from tick to tick.
    /// </summary>
    public abstract class TickNode : Node
    {
        /// <summary>
        /// The output giving the seconds since the start, at each tick.
        /// </summary>
        public const string SecondsOutput = "seconds";

        /// <summary>
        /// On the node, and where a stream's values come from: "every 2 s", "at each press".
        /// </summary>
        public abstract string Label { get; }

        /// <summary>
        /// What a tick is called when a run says how far it got: "tick", "press".
        /// </summary>
        public abstract string TickName { get; }

        /// <summary>
        /// Whether the stream ends by itself.
        /// </summary>
        public virtual bool Ends => false;

        /// <summary>
        /// The Julia code the ticks come from, for runs and exported scripts alike.
        /// </summary>
        public abstract string PreludeCode { get; }

        /// <summary>
        /// The line starting the loop through the ticks: a call ticking, <c>do _path, _n, _show</c> (<c>_path</c> being the
        /// seconds since the start), its body the nodes after it.
        /// </summary>
        /// <param name="stopFile">For a run: the file whose appearing ends the stream; the run is told when it starts.
        /// Null for an exported script.</param>
        public abstract string Start(string? stopFile);

        /// <summary>
        /// The call telling the run the stream has started, for <see cref="Start"/>'s ready.
        /// </summary>
        protected string Ready => $"() -> _jsynth_watching(\"{ID}\")";

        public override string OutputType => "Int64";

        public override string OutputTypeOf(string? output) => output == SecondsOutput ? "Float64" : OutputType;

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property == nameof(Label);
        }
    }
}
