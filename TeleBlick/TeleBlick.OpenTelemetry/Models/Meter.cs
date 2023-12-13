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
    }
}
