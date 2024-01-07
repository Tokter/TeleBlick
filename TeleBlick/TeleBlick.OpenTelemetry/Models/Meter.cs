using OpenTelemetry.Proto.Common.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    [DebuggerDisplay("MeterName = {MeterName}")]
    public class Meter
    {
        public string MeterName { get; init; }
        public string Version { get; init; }
        public Dictionary<string, string> Properties { get; }

        public Meter(InstrumentationScope scope)
        {
            MeterName = scope.Name;
            Version = scope.Version;
            Properties = scope.Attributes.ToDictionary();
        }

        public Meter(BinaryReader reader)
        {
            MeterName = reader.ReadString();
            Version = reader.ReadString();

            //Read the properties
            var count = reader.ReadInt32();
            Properties = new Dictionary<string, string>(count);
            for (var i = 0; i < count; i++)
            {
                Properties.Add(reader.ReadString(), reader.ReadString());
            }
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(MeterName);
            writer.Write(Version);

            //Write the properties
            writer.Write(Properties.Count);
            foreach (var property in Properties)
            {
                writer.Write(property.Key);
                writer.Write(property.Value);
            }
        }
    }
}
