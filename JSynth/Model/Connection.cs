using Newtonsoft.Json;

namespace JSynth.Model
{
    /// <summary>
    /// A wire from the output of one node to a parameter (input) of a <see cref="Function"/>.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Connection
    {
        /// <summary>
        /// <see cref="Node.ID"/> of the node whose output feeds the wire.
        /// </summary>
        [JsonProperty]
        public string SourceID { get; private set; }

        /// <summary>
        /// <see cref="Node.ID"/> of the function receiving the value.
        /// </summary>
        [JsonProperty]
        public string TargetID { get; private set; }

        /// <summary>
        /// <see cref="Parameter.ID"/> of the receiving parameter.
        /// </summary>
        [JsonProperty]
        public string ParameterID { get; private set; }

        /// <summary>
        /// Which of the source's outputs feeds the wire, for a node with more than its value (a Merge node's folder);
        /// null for its value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Output { get; private set; }

        /// <summary>
        /// Where a run pauses on this wire; null for none. Set through MainViewModel.SetBreakpoint, which keeps the
        /// design's history.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Breakpoint? Breakpoint { get; set; }

        /// <summary>
        /// What tells the wire apart from the others: its source, its output and the input it goes into.
        /// </summary>
        public string Key => $"{SourceID}:{Output}:{ParameterID}";

        [JsonConstructor]
        public Connection(string sourceID, string targetID, string parameterID, string? output = null)
        {
            SourceID = sourceID;
            TargetID = targetID;
            ParameterID = parameterID;
            Output = output;
        }
    }
}
