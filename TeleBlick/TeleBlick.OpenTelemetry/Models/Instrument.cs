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
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Unit { get; init; }
        public required InstrumentKind Type { get; init; }
        public required Meter Parent { get; init; }

        public Dictionary<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> Dimensions { get; } = new(ScopeAttributesComparer.Instance);
        public Dictionary<string, List<string>> KnownAttributeValues { get; } = new();

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

        private DimensionScope FindScope(RepeatedField<KeyValue> attributes, ref KeyValuePair<string, string>[]? tempAttributes)
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
