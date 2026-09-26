using Newtonsoft.Json;

namespace JuliaSynth.Model
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

        [JsonConstructor]
        public Connection(string sourceID, string targetID, string parameterID)
        {
            SourceID = sourceID;
            TargetID = targetID;
            ParameterID = parameterID;
        }
    }
}
