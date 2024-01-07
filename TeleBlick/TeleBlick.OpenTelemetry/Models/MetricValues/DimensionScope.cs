using OpenTelemetry.Proto.Metrics.V1;
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

        public DimensionScope(BinaryReader reader)
        {
            Name = reader.ReadString();

            //Read the attributes
            var count = reader.ReadInt32();
            Attributes = new KeyValuePair<string, string>[count];
            for (var i = 0; i < count; i++)
            {
                Attributes[i] = new KeyValuePair<string, string>(reader.ReadString(), reader.ReadString());
            }

            //Read the values
            count = reader.ReadInt32();
            Values = new List<MetricValueBase>(count);
            for (var i = 0; i < count; i++)
            {
                var type = reader.ReadInt32();
                switch (type)
                {
                    case 1:
                        Values.Add(new MetricValue<long>(reader.ReadInt64(), DateTime.FromBinary(reader.ReadInt64()), DateTime.FromBinary(reader.ReadInt64())));
                        break;
                    case 2:
                        Values.Add(new MetricValue<double>(reader.ReadDouble(), DateTime.FromBinary(reader.ReadInt64()), DateTime.FromBinary(reader.ReadInt64())));
                        break;
                    case 3:
                        var sum = reader.ReadDouble();
                        var count2 = reader.ReadUInt64();
                        var start = DateTime.FromBinary(reader.ReadInt64());
                        var end = DateTime.FromBinary(reader.ReadInt64());
                        var explicitBounds = new double[reader.ReadInt32()];
                        for (var j = 0; j < explicitBounds.Length; j++)
                        {
                            explicitBounds[j] = reader.ReadDouble();
                        }
                        var values = new ulong[reader.ReadInt32()];
                        for (var j = 0; j < values.Length; j++)
                        {
                            values[j] = reader.ReadUInt64();
                        }
                        Values.Add(new HistogramValue(values, sum, count2, start, end, explicitBounds));
                        break;
                    default:
                        throw new Exception($"Unknown metric value type {type}!");
                }
            }
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

        internal static DimensionScope Clone(DimensionScope value, DateTime? valuesStart, DateTime? valuesEnd)
        {
            var newDimensionScope = new DimensionScope(value.Attributes);
            if (valuesStart != null && valuesEnd != null)
            {
                foreach (var item in value.Values)
                {
                    if ((item.Start <= valuesEnd.Value && item.End >= valuesStart.Value) ||
                        (item.Start >= valuesStart.Value && item.End <= valuesEnd.Value))
                    {
                        newDimensionScope.Values.Add(MetricValueBase.Clone(item));
                    }
                }
            }
            else
            {
                foreach (var item in value.Values)
                {
                    newDimensionScope.Values.Add(MetricValueBase.Clone(item));
                }
            }


            return newDimensionScope;
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Name);

            //Write the attributes
            writer.Write(Attributes.Length);
            foreach (var attribute in Attributes)
            {
                writer.Write(attribute.Key);
                writer.Write(attribute.Value);
            }

            //Write the values
            writer.Write(Values.Count);
            foreach (var value in Values)
            {
                if (value is MetricValue<long> longValue)
                {
                    writer.Write(1);
                    writer.Write(longValue.Value);
                    writer.Write(longValue.Count);
                    writer.Write(longValue.Start.ToBinary());
                    writer.Write(longValue.End.ToBinary());
                }
                else if (value is MetricValue<double> doubleValue)
                {
                    writer.Write(2);
                    writer.Write(doubleValue.Value);
                    writer.Write(doubleValue.Count);
                    writer.Write(doubleValue.Start.ToBinary());
                    writer.Write(doubleValue.End.ToBinary());
                }
                else if (value is HistogramValue histogramValue)
                {
                    writer.Write(3);
                    writer.Write(histogramValue.Sum);
                    writer.Write(histogramValue.Count);
                    writer.Write(histogramValue.Start.ToBinary());
                    writer.Write(histogramValue.End.ToBinary());
                    writer.Write(histogramValue.ExplicitBounds.Length);
                    foreach (var bound in histogramValue.ExplicitBounds)
                    {
                        writer.Write(bound);
                    }
                    writer.Write(histogramValue.Values.Length);
                    foreach (var bucket in histogramValue.Values)
                    {
                        writer.Write(bucket);
                    }
                }
                else
                {
                    throw new Exception($"Unknown metric value type {value.GetType().Name}!");
                }
            }
        }
    }
}
