using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    public class SpanEvent
    {
        public SpanEvent(string name, DateTime time, Dictionary<string,string> attributes)
        {
            Name = name;
            Time = time;
            Attributes = attributes;
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        /// <param name="reader"></param>
        public SpanEvent(BinaryReader reader)
        {
            Name = reader.ReadString();
            Time = DateTime.FromBinary(reader.ReadInt64());
            var count = reader.ReadInt32();
            Attributes = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                Attributes.Add(reader.ReadString(), reader.ReadString());
            }
        }

        public string Name { get; private set; }
        public DateTime Time { get; private set; }
        public Dictionary<string,string> Attributes { get; private set; }
        public double TimeOffset(Span span) => (Time - span.StartTime).TotalMilliseconds;

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Time.ToBinary());
            writer.Write(Attributes.Count);
            foreach (var attribute in Attributes)
            {
                writer.Write(attribute.Key);
                writer.Write(attribute.Value);
            }
        }
    }
}
