using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    /// <summary>
    /// A resource represents the entity producing telemetry as resource attributes. For example, a process producing telemetry that is running
    /// in a container on Kubernetes has a process name, a pod name, a namespace, and possibly a deployment name. All four of these attributes
    /// can be included in the resource.
    /// In your observability backend, you can use resource information to better investigate interesting behavior.For example, if your trace
    /// or metrics data indicate latency in your system, you can narrow it down to a specific container, pod, or Kubernetes deployment.
    /// If you use Jaeger as your observability backend, resource attributes are grouped under the Process tab:
    /// https://opentelemetry.io/docs/concepts/resources/
    /// </summary>
    [DebuggerDisplay("ApplicationName = {ApplicationName}, InstanceId = {InstanceId}")]
    public class Application
    {
        public const string SERVICE_NAME = "service.name";
        public const string SERVICE_INSTANCE_ID = "service.instance.id";

        public string ApplicationName { get; }
        public string InstanceId { get; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        private readonly ReaderWriterLockSlim _metricsLock = new();
        private readonly Dictionary<string, Meter> _meters = new();
        private readonly Dictionary<InstrumentKey, Instrument> _instruments = new();

        public Application(Resource resource)
        {
            foreach (var attribute in resource.Attributes)
            {
                switch (attribute.Key)
                {
                    case SERVICE_NAME:
                        ApplicationName = attribute.Value.GetString();
                        break;

                    case SERVICE_INSTANCE_ID:
                        InstanceId = attribute.Value.GetString();
                        break;
                }
                Properties.Add(attribute.Key, attribute.Value.GetString());
            }

            if (string.IsNullOrEmpty(ApplicationName))
            {
                ApplicationName = "Unknown";
            }

            if (string.IsNullOrEmpty(InstanceId))
            {
                InstanceId = ApplicationName;
            }
        }

        public Application(BinaryReader reader)
        {
            ApplicationName = reader.ReadString();
            InstanceId = reader.ReadString();

            //Read the properties
            var propertyCount = reader.ReadInt32();
            for (var i = 0; i < propertyCount; i++)
            {
                Properties.Add(reader.ReadString(), reader.ReadString());
            }

            //Read the meters
            var meterCount = reader.ReadInt32();
            for (var i = 0; i < meterCount; i++)
            {
                var meter = new Meter(reader);
                _meters.Add(meter.MeterName, meter);
            }

            //Read the instruments
            var instrumentCount = reader.ReadInt32();
            for (var i = 0; i < instrumentCount; i++)
            {
                var instrumentKey = new InstrumentKey(reader.ReadString(), reader.ReadString());
                _instruments.Add(instrumentKey, new Instrument(reader, (string key) => _meters[key]));
            }
        }


        public void AddMetrics(AddContext context, RepeatedField<ScopeMetrics> scopeMetrics)
        {
            _metricsLock.EnterWriteLock();

            try
            {
                // Temporary attributes array to use when adding metrics to the instruments.
                KeyValuePair<string, string>[]? tempAttributes = null;

                foreach (var sm in scopeMetrics)
                {
                    foreach (var metric in sm.Metrics)
                    {
                        try
                        {
                            var instrumentKey = new InstrumentKey(sm.Scope.Name, metric.Name);
                            if (!_instruments.TryGetValue(instrumentKey, out var instrument))
                            {
                                _instruments.Add(instrumentKey, instrument = new Instrument(metric.Name, metric.Description, metric.Unit, MapMetricType(metric.DataCase), GetMeter(sm.Scope)));
                            }

                            instrument.AddMetrics(metric, ref tempAttributes);
                        }
                        catch (Exception)
                        {
                            context.FailureCount++;
                        }
                    }
                }
            }
            finally
            {
                _metricsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Returns the specified instrument, with the meter values.
        /// </summary>
        /// <param name="meterName"></param>
        /// <param name="instrumentName"></param>
        /// <param name="valuesStart"></param>
        /// <param name="valuesEnd"></param>
        /// <returns></returns>
        public Instrument? GetInstrument(string meterName, string instrumentName, DateTime? valuesStart, DateTime? valuesEnd)
        {
            _metricsLock.EnterReadLock();

            try
            {
                if (!_instruments.TryGetValue(new InstrumentKey(meterName, instrumentName), out var instrument))
                {
                    return null;
                }

                return Instrument.Clone(instrument, cloneData: true, valuesStart: valuesStart, valuesEnd: valuesEnd);
            }
            finally
            {
                _metricsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns a list of instruments, but without the meter values.
        /// </summary>
        /// <returns>A list of instruments.</returns>
        public List<Instrument> GetInstrumentsSummary()
        {
            _metricsLock.EnterReadLock();

            try
            {
                var instruments = new List<Instrument>(_instruments.Count);
                foreach (var instrument in _instruments)
                {
                    instruments.Add(Instrument.Clone(instrument.Value, cloneData: false, valuesStart: null, valuesEnd: null));
                }
                return instruments;
            }
            finally
            {
                _metricsLock.ExitReadLock();
            }
        }

        public void Write(BinaryWriter writer)
        {
            try
            {
                _metricsLock.EnterReadLock();

                writer.Write(ApplicationName);
                writer.Write(InstanceId);

                //Write the properties
                writer.Write(Properties.Count);
                foreach (var property in Properties)
                {
                    writer.Write(property.Key);
                    writer.Write(property.Value);
                }

                //Write the meters
                writer.Write(_meters.Count);
                foreach (var meter in _meters.Values)
                {
                    meter.Write(writer);
                }

                //Write the instruments
                writer.Write(_instruments.Count);
                foreach (var instrumentKey in _instruments.Keys)
                {
                    writer.Write(instrumentKey.MeterName);
                    writer.Write(instrumentKey.InstrumentName);
                    var instrument = _instruments[instrumentKey];
                    instrument.Write(writer);
                }
            }
            finally
            {
                _metricsLock.ExitReadLock();
            }
        }

        private static InstrumentKind MapMetricType(Metric.DataOneofCase data)
        {
            return data switch
            {
                Metric.DataOneofCase.Gauge => InstrumentKind.Gauge,
                Metric.DataOneofCase.Sum => InstrumentKind.Sum,
                Metric.DataOneofCase.Histogram => InstrumentKind.Histogram,
                Metric.DataOneofCase.Summary => InstrumentKind.Summary,
                _ => InstrumentKind.Unsupported
            };
        }

        private Meter GetMeter(InstrumentationScope scope)
        {
            if (!_meters.TryGetValue(scope.Name, out var meter))
            {
                _meters.Add(scope.Name, meter = new Meter(scope));
            }
            return meter;
        }
    }
}
