using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models.MetricValues
{
    [DebuggerDisplay("Start = {Start}, End = {End}")]
    public abstract class MetricValueBase
    {
        public readonly DateTime Start;
        public DateTime End { get; set; }
        public ulong Count = 1;

        protected MetricValueBase(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }

        internal static MetricValueBase Clone(MetricValueBase item)
        {
            return item.Clone();
        }

        protected abstract MetricValueBase Clone();
    }
}
