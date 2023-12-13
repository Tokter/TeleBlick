// See https://aka.ms/new-console-template for more information
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace OpenTelemetryTest;

class Program
{
    private readonly static ActivitySource MyActivitySource = new("TestSource");

    static void Main(string[] args)
    {
        Console.WriteLine("Press [Enter] to create OpenTelemetry trace...");

        Console.ReadLine();

        Console.WriteLine("Creating trace...");

        var application = ResourceBuilder.CreateDefault()
                .AddService("Test Service", serviceNamespace: "Tokter")
                .AddAttributes(new Dictionary<string, object>
                {
                    { "service.instance.id", Guid.NewGuid().ToString() },
                    { "service.version", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0" },
                });

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddConsoleExporter()
            .AddOtlpExporter(ConfigureOtlpOptions)

            //Setup the Application information
            .SetResourceBuilder(application)
            .Build();

        var meters = new Meters();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter(meters.Name)
            .AddConsoleExporter()
            .AddOtlpExporter(ConfigureOtlpOptions)
            //Setup the Application information
            .SetResourceBuilder(application)
            .Build();


        for (int i = 0; i < 10; i++)
        {
            ParentOperation(meters);
            GC.Collect(1);
        }

        try
        {
            throw new Exception("Oops!");
        }
        catch (Exception)
        {
            // swallow the exception
        }

        // Dispose tracer provider before the application ends.
        // This will flush the remaining spans and shutdown the tracing pipeline.
        tracerProvider.Dispose();
        meterProvider.Dispose();

        Console.WriteLine("Done!");
    }

    private static void ConfigureOtlpOptions(OtlpExporterOptions configure)
    {
        configure.Endpoint = new Uri("http://localhost:55678");
        configure.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        configure.ExportProcessorType = ExportProcessorType.Batch;
    }

    public static void ParentOperation(Meters meters)
    {
        using var parentActivity = MyActivitySource.StartActivity("ParentActivity");
        ChildOperation(meters);
        parentActivity?.SetTag("foo", 1);
        parentActivity?.SetTag("bar", "Hello, World!");
        parentActivity?.SetTag("baz", new int[] { 1, 2, 3 });
        parentActivity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static void ChildOperation(Meters meters)
    {
        using var childActivity = MyActivitySource.StartActivity("ChildActivity");

        meters.ChildOperationCounterAdd(1, "Test", 123);

        //Wait a random amount between 100 and 1000ms
        Thread.Sleep(new Random().Next(100, 1000));
    }

    public class Meters
    {
        private readonly Meter _meter;
        private readonly Counter<int> _childOperationCounter;

        public string Name => _meter.Name;

        public Meters()
        {
            _meter = new Meter("TestMeter");
            _childOperationCounter = _meter.CreateCounter<int>("ChildOperation.Calls", description: "Numer of times we call the ChildOperation method.");
        }

        public void ChildOperationCounterAdd(int value)
        {
            _childOperationCounter.Add(value);
        }

        public void ChildOperationCounterAdd(int value, string tagName, object tagValue)
        {
            var tag = new KeyValuePair<string, object?>(tagName, tagValue);
            _childOperationCounter.Add(value, tag);
        }
    }
}

