using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    public class SpanEvent
    {
        public required string Name { get; init; }
        public required DateTime Time { get; init; }
        public required Dictionary<string,string> Attributes { get; init; }
        public double TimeOffset(Span span) => (Time - span.StartTime).TotalMilliseconds;
    }
}
