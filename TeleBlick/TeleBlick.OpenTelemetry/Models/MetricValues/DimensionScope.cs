﻿using OpenTelemetry.Proto.Metrics.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models.MetricValues
{
    [DebuggerDisplay("Name = {Name}, Values = {Values.Count}")]
    public class DimensionScope
    {
        public string Name { get; init; }
        public KeyValuePair<string, string>[] Attributes { get; init; }
        public readonly List<MetricValueBase> Values = new();

        // Used to aid in merging values that are the same in a concurrent environment
        private MetricValueBase? _lastValue;

        public DimensionScope(KeyValuePair<string, string>[] attributes)
        {
            Attributes = attributes;
            var name = Attributes.ConcatProperties();
            Name = name != null && name.Length > 0 ? name : "no-dimensions";
        }

        /// <summary>
        /// Compares and updates the timespan for metrics if they are unchanged.
        /// </summary>
        /// <param name="d">Metric value to merge</param>
        public void AddPointValue(NumberDataPoint d)
        {
            var start = d.StartTimeUnixNano.ToDateTime();
            var end = d.TimeUnixNano.ToDateTime();

            if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsInt)
            {
                var value = d.AsInt;
                var lastLongValue = _lastValue as MetricValue<long>;
                if (lastLongValue is not null && lastLongValue.Value == value)
                {
                    lastLongValue.End = end;
                    Interlocked.Increment(ref lastLongValue.Count);
                }
                else
                {
                    if (lastLongValue is not null)
                    {
                        start = lastLongValue.End;
                    }
                    _lastValue = new MetricValue<long>(d.AsInt, start, end);
                    Values.Add(_lastValue);
                }
            }
            else if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsDouble)
            {
                var lastDoubleValue = _lastValue as MetricValue<double>;
                if (lastDoubleValue is not null && lastDoubleValue.Value == d.AsDouble)
                {
                    lastDoubleValue.End = end;
                    Interlocked.Increment(ref lastDoubleValue.Count);
                }
                else
                {
                    if (lastDoubleValue is not null)
                    {
                        start = lastDoubleValue.End;
                    }
                    _lastValue = new MetricValue<double>(d.AsDouble, start, end);
                    Values.Add(_lastValue);
                }
            }
        }

        public void AddHistogramValue(HistogramDataPoint h)
        {
            var start = h.StartTimeUnixNano.ToDateTime();
            var end = h.TimeUnixNano.ToDateTime();

            var lastHistogramValue = _lastValue as HistogramValue;
            if (lastHistogramValue is not null && lastHistogramValue.Count == h.Count)
            {
                lastHistogramValue.End = end;
            }
            else
            {
                // If the explicit bounds are the same as the last value, reuse them.
                double[] explicitBounds;
                if (lastHistogramValue is not null)
                {
                    start = lastHistogramValue.End;
                    explicitBounds = lastHistogramValue.ExplicitBounds.SequenceEqual(h.ExplicitBounds)
                        ? lastHistogramValue.ExplicitBounds
                        : h.ExplicitBounds.ToArray();
                }
                else
                {
                    explicitBounds = h.ExplicitBounds.ToArray();
                }
                _lastValue = new HistogramValue(h.BucketCounts.ToArray(), h.Sum, h.Count, start, end, explicitBounds);
                Values.Add(_lastValue);
            }
        }

    }
}