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
                                _instruments.Add(instrumentKey, instrument = new Instrument
                                {
                                    Name = metric.Name,
                                    Description = metric.Description,
                                    Unit = metric.Unit,
                                    Type = MapMetricType(metric.DataCase),
                                    Parent = GetMeter(sm.Scope)
                                });
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

                ////Write the meters
                //writer.Write(_meters.Count);
                //foreach (var meter in _meters.Values)
                //{
                //    meter.Serialize(writer);
                //}

                ////Write the instruments
                //writer.Write(_instruments.Count);
                //foreach (var instrument in _instruments.Values)
                //{
                //    instrument.Serialize(writer);
                //}
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
