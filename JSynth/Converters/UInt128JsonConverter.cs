using Newtonsoft.Json;
using System;
using System.Globalization;

namespace JSynth.Converters
{
    /// <summary>
    /// Newtonsoft.Json has no built-in support for <see cref="UInt128"/>; store it as a decimal string.
    /// </summary>
    public class UInt128JsonConverter : JsonConverter<UInt128>
    {
        public override void WriteJson(JsonWriter writer, UInt128 value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        }

        public override UInt128 ReadJson(JsonReader reader, Type objectType, UInt128 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value switch
            {
                null => UInt128.Zero,
                string s => UInt128.Parse(s, CultureInfo.InvariantCulture),
                long l => (UInt128)l,
                System.Numerics.BigInteger b => (UInt128)b,
                object o => UInt128.Parse(Convert.ToString(o, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
            };
        }
    }
}
