using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models.MetricValues
{
    public class MetricValue<T> : MetricValueBase where T : struct
    {
        public readonly T Value;

        public MetricValue(T value, DateTime start, DateTime end) : base(start, end)
        {
            Value = value;
        }

        public override string? ToString() => Value.ToString();
    }
}
