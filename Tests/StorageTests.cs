using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using TeleBlick.OpenTelemetry;
using TeleBlick.OpenTelemetry.Models;
using TeleBlick.OpenTelemetry.Models.MetricValues;

namespace Tests
{
    public class StorageTests
    {
        private const string SERVICE_NAME_KEY = "service.name";
        private const string SERVICE_NAME_VALUE = "TestApplication";
        private const string INSTANCE_ID_KEY = "service.instance.id";
        private const string INSTANCE_ID_VALUE = "123";
        private const string PROPERTY_KEY = "property.key";
        private const string PROPERTY_VALUE = "TestValue";
        private const string INSTR_SCOPE_NAME = "TestScope";
        private const string INSTR_SCOPE_VERSION = "1.2.3";
        private const string INSTR_METRIC_HISTOGRAM_NAME = "TestHistogram";
        private const string INSTR_METRIC_HISTOGRAM_DESC = "Test Histogram Desc";
        private const string INSTR_METRIC_HISTOGRAM_UNIT = "MS";
        private DateTime INSTR_METRIC_HISTOGRAM_START = new DateTime(2023, 3, 12, 8, 0, 0, DateTimeKind.Utc);
        private DateTime INSTR_METRIC_HISTOGRAM_TIME = new DateTime(2023, 3, 12, 9, 0, 0, DateTimeKind.Utc);
        private const ulong INSTR_METRIC_HISTOGRAM_COUNT = 3;

        [Fact]
        public void Applications()
        {
            var storage = new TelemetryStorage();
            storage.GetOrAddApplication(GetApplicationResource());

            TelemetryStorage storage2 = WriteToMemoryAndReadBack(storage);

            var application = storage2.GetApplications().First();
            Assert.Equal(SERVICE_NAME_VALUE, application.ApplicationName);
            Assert.Equal(INSTANCE_ID_VALUE, application.InstanceId);
            Assert.Equal(3, application.Properties.Count);
            foreach (var p in application.Properties)
            {
                switch (p.Key)
                {
                    case SERVICE_NAME_KEY:
                        Assert.Equal(SERVICE_NAME_VALUE, p.Value);
                        break;
                    case INSTANCE_ID_KEY:
                        Assert.Equal(INSTANCE_ID_VALUE, p.Value);
                        break;
                    case PROPERTY_KEY:
                        Assert.Equal(PROPERTY_VALUE, p.Value);
                        break;
                    default:
                        Assert.Fail($"Unknown property key: {p.Key}");
                        break;
                }
            }
        }

        [Fact]
        public void ApplicationMetrics()
        {
            var storage = new TelemetryStorage();
            var list = new RepeatedField<ResourceMetrics>();
            list.Add(GetMetrics());
            storage.AddMetrics(new AddContext(), list);

            TelemetryStorage storage2 = WriteToMemoryAndReadBack(storage);

            var application = storage2.GetApplications().First();

            Assert.Collection(
                application.GetInstrumentsSummary(),
                    instrumentSummary =>
                    {
                        Assert.Equal(INSTR_SCOPE_NAME, instrumentSummary.Parent.MeterName);
                        Assert.Equal(INSTR_METRIC_HISTOGRAM_NAME, instrumentSummary.Name);
                        Assert.Equal(INSTR_METRIC_HISTOGRAM_DESC, instrumentSummary.Description);
                        Assert.Equal(INSTR_METRIC_HISTOGRAM_UNIT, instrumentSummary.Unit);

                        var instrument = application.GetInstrument(instrumentSummary.Parent.MeterName, instrumentSummary.Name, null, null);
                        Assert.NotNull(instrument);
                        Assert.Equal(INSTR_SCOPE_NAME, instrument.Parent.MeterName);

                        switch (instrument.Type)
                        {
                            case InstrumentKind.Histogram:
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_NAME, instrument.Name);
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_DESC, instrument.Description);
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_UNIT, instrument.Unit);

                                var histogram = instrument.Dimensions[new KeyValuePair<string, string>[] { KeyValuePair.Create(PROPERTY_KEY, PROPERTY_VALUE) }];
                                var histogramPoint = (HistogramValue)histogram.Values[0];
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_START, histogramPoint.Start);
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_TIME, histogramPoint.End);
                                Assert.Equal(INSTR_METRIC_HISTOGRAM_COUNT, histogramPoint.Count);
                                break;

                            default:
                                Assert.Fail($"Unknown instrument type: {instrument.Type}");
                                break;
                        }
                    }
            );
        }

        private TelemetryStorage WriteToMemoryAndReadBack(TelemetryStorage storage)
        {
            TelemetryStorage result;

            //Write storage to memory stream
            using (var stream = new MemoryStream())
            {
                storage.Write(stream);

                //Read storage from memory stream
                stream.Position = 0;
                result = new TelemetryStorage(stream);
            }
            return result;
        }

        private Resource GetApplicationResource()
        {
            var resource = new Resource();
            resource.Attributes.Add(GetKeyValue(SERVICE_NAME_KEY, SERVICE_NAME_VALUE));
            resource.Attributes.Add(GetKeyValue(INSTANCE_ID_KEY, INSTANCE_ID_VALUE));
            resource.Attributes.Add(GetKeyValue(PROPERTY_KEY, PROPERTY_VALUE));
            return resource;
        }

        private ResourceMetrics GetMetrics()
        {
            var result = new ResourceMetrics();
            result.Resource = GetApplicationResource();
            result.ScopeMetrics.Add(GetMetricScope(GetHistogramMetric()));
            return result;
        }

        private ScopeMetrics GetMetricScope(Metric metric)
        {
            var result = new ScopeMetrics();
            
            //Create Scope
            result.Scope = new InstrumentationScope();
            result.Scope.Name = INSTR_SCOPE_NAME;
            result.Scope.Version = INSTR_SCOPE_VERSION;
            result.Scope.Attributes.Add(GetKeyValue(PROPERTY_KEY, PROPERTY_VALUE));

            //Add metric to scope
            result.Metrics.Add(metric);

            return result;
        }

        private Metric GetHistogramMetric()
        {
            var result = new Metric();
            result.Name = INSTR_METRIC_HISTOGRAM_NAME;
            result.Description = INSTR_METRIC_HISTOGRAM_DESC;
            result.Unit = INSTR_METRIC_HISTOGRAM_UNIT;
            result.Histogram = new Histogram();

            var point1 = new HistogramDataPoint();
            point1.StartTimeUnixNano = INSTR_METRIC_HISTOGRAM_START.ToUnixNanoseconds();
            point1.TimeUnixNano = INSTR_METRIC_HISTOGRAM_TIME.ToUnixNanoseconds();
            point1.Count = INSTR_METRIC_HISTOGRAM_COUNT;
            point1.Attributes.Add(GetKeyValue(PROPERTY_KEY, PROPERTY_VALUE));

            result.Histogram.DataPoints.Add(point1);
            return result;
        }



        private OpenTelemetry.Proto.Common.V1.KeyValue GetKeyValue(string key, string value)
        {
            return new OpenTelemetry.Proto.Common.V1.KeyValue
            {
                Key = key,
                Value = new OpenTelemetry.Proto.Common.V1.AnyValue
                {
                    StringValue = value
                }
            };
        }

    }
}