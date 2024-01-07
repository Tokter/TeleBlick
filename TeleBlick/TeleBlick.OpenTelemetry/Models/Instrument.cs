using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleBlick.OpenTelemetry.Models.MetricValues;

namespace TeleBlick.OpenTelemetry.Models
{
    public enum InstrumentKind
    {
        Unsupported,
        Gauge,
        Sum,
        Histogram,
        Summary
    }

    public record struct InstrumentKey(string MeterName, string InstrumentName);

    /// <summary>
    /// In OpenTelemetry measurements are captured by metric instruments. A metric instrument is defined by:
    /// https://opentelemetry.io/docs/concepts/signals/metrics/#metric-instruments
    /// </summary>
    [DebuggerDisplay("Name = {Name}, Unit = {Unit}, Type = {Type}")]
    public class Instrument
    {
        public string Name { get; init; }
        public string Description { get; init; }
        public string Unit { get; init; }
        public InstrumentKind Type { get; init; }
        public Meter Parent { get; init; }

        public Dictionary<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> Dimensions { get; } = new(ScopeAttributesComparer.Instance);
        public Dictionary<string, List<string>> KnownAttributeValues { get; } = new();

        public Instrument(string name, string description, string unit, InstrumentKind type, Meter parent)
        {
            Name = name;
            Description = description;
            Unit = unit;
            Type = type;
            Parent = parent;
        }

        public Instrument(BinaryReader reader, Func<string,Meter> meterLookup)
        {
            Name = reader.ReadString();
            Description = reader.ReadString();
            Unit = reader.ReadString();
            Type = (InstrumentKind)reader.ReadInt32();
            Parent = meterLookup(reader.ReadString());

            //Read the known attribute values
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var valueCount = reader.ReadInt32();
                var values = new List<string>(valueCount);
                for (var j = 0; j < valueCount; j++)
                {
                    values.Add(reader.ReadString());
                }
                KnownAttributeValues.Add(key, values);
            }

            //Read the dimensions
            count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var attributeCount = reader.ReadInt32();
                var attributes = new KeyValuePair<string, string>[attributeCount];
                for (var j = 0; j < attributeCount; j++)
                {
                    attributes[j] = new KeyValuePair<string, string>(reader.ReadString(), reader.ReadString());
                }
                Dimensions.Add(attributes, new DimensionScope(reader));
            }
        }

        public void AddMetrics(Metric metric, ref KeyValuePair<string, string>[]? tempAttributes)
        {
            switch (metric.DataCase)
            {
                case Metric.DataOneofCase.Gauge:
                    foreach (var d in metric.Gauge.DataPoints)
                    {
                        FindScope(d.Attributes, ref tempAttributes).AddPointValue(d);
                    }
                    break;

                case Metric.DataOneofCase.Sum:
                    foreach (var d in metric.Sum.DataPoints)
                    {
                        FindScope(d.Attributes, ref tempAttributes).AddPointValue(d);
                    }
                    break;

                case Metric.DataOneofCase.Histogram:
                    foreach (var d in metric.Histogram.DataPoints)
                    {
                        FindScope(d.Attributes, ref tempAttributes).AddHistogramValue(d);
                    }
                    break;

                //case Metric.DataOneofCase.Summary:
                //    foreach (var d in metric.Summary.DataPoints)
                //    {
                //        FindScope(d.Attributes, ref tempAttributes).AddSummaryValue(d);
                //    }
                //    break;
            }
        }

        public InstrumentKey GetKey() => new(Parent.MeterName, Name);

        public DimensionScope FindScope(RepeatedField<KeyValue> attributes, ref KeyValuePair<string, string>[]? tempAttributes)
        {
            // We want to find the dimension scope that matches the attributes, but we don't want to allocate.
            // Copy values to a temporary reusable array.
            attributes.CopyKeyValuePairs(ref tempAttributes);
            Array.Sort(tempAttributes, 0, attributes.Count, KeyValuePairComparer.Instance);

            var comparableAttributes = tempAttributes.AsMemory(0, attributes.Count);

            if (!Dimensions.TryGetValue(comparableAttributes, out var dimension))
            {
                dimension = AddDimensionScope(comparableAttributes);
            }
            return dimension;
        }

        private DimensionScope AddDimensionScope(Memory<KeyValuePair<string, string>> comparableAttributes)
        {
            var isFirst = Dimensions.Count == 0;
            var durableAttributes = comparableAttributes.ToArray();
            var dimension = new DimensionScope(durableAttributes);
            Dimensions.Add(durableAttributes, dimension);

            var keys = KnownAttributeValues.Keys.Union(durableAttributes.Select(a => a.Key)).Distinct();
            foreach (var key in keys)
            {
                if (!KnownAttributeValues.TryGetValue(key, out var values))
                {
                    KnownAttributeValues.Add(key, values = new List<string>());

                    // If the key is new and there are already dimensions, add an empty value because there are dimensions without this key.
                    if (!isFirst)
                    {
                        TryAddValue(values, string.Empty);
                    }
                }

                var currentDimensionValue = durableAttributes.GetValue(key);
                TryAddValue(values, currentDimensionValue ?? string.Empty);
            }

            return dimension;

            static void TryAddValue(List<string> values, string value)
            {
                if (!values.Contains(value))
                {
                    values.Add(value);
                }
            }
        }

        public static Instrument Clone(Instrument instrument, bool cloneData, DateTime? valuesStart, DateTime? valuesEnd)
        {
            var newInstrument = new Instrument(instrument.Name, instrument.Description, instrument.Unit, instrument.Type, instrument.Parent);

            if (cloneData)
            {
                foreach (var item in instrument.KnownAttributeValues)
                {
                    newInstrument.KnownAttributeValues.Add(item.Key, item.Value.ToList());
                }
                foreach (var item in instrument.Dimensions)
                {
                    newInstrument.Dimensions.Add(item.Key, DimensionScope.Clone(item.Value, valuesStart, valuesEnd));
                }
            }

            return newInstrument;
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Description);
            writer.Write(Unit);
            writer.Write((int)Type);
            writer.Write(Parent.MeterName);

            //Write the known attribute values
            writer.Write(KnownAttributeValues.Count);
            foreach (var knownAttribute in KnownAttributeValues)
            {
                writer.Write(knownAttribute.Key);
                writer.Write(knownAttribute.Value.Count);
                foreach (var value in knownAttribute.Value)
                {
                    writer.Write(value);
                }
            }

            //Write the dimensions
            writer.Write(Dimensions.Count);
            foreach (var dimension in Dimensions)
            {
                writer.Write(dimension.Key.Length);
                foreach (var attribute in dimension.Key.Span)
                {
                    writer.Write(attribute.Key);
                    writer.Write(attribute.Value);
                }
                dimension.Value.Write(writer);
            }
        }

        private sealed class ScopeAttributesComparer : IEqualityComparer<ReadOnlyMemory<KeyValuePair<string, string>>>
        {
            public static readonly ScopeAttributesComparer Instance = new();

            public bool Equals(ReadOnlyMemory<KeyValuePair<string, string>> x, ReadOnlyMemory<KeyValuePair<string, string>> y)
            {
                return x.Span.SequenceEqual(y.Span);
            }

            public int GetHashCode([DisallowNull] ReadOnlyMemory<KeyValuePair<string, string>> obj)
            {
                var hashcode = new HashCode();
                foreach (KeyValuePair<string, string> pair in obj.Span)
                {
                    hashcode.Add(pair.Key);
                    hashcode.Add(pair.Value);
                }
                return hashcode.ToHashCode();
            }
        }

        private sealed class KeyValuePairComparer : IComparer<KeyValuePair<string, string>>
        {
            public static readonly KeyValuePairComparer Instance = new();

            public int Compare(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
            {
                return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
            }
        }
    }
}
