using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTelemetry.Proto.Collector.Trace.V1.TraceService;

namespace TeleBlick.OpenTelemetry.Services
{
    public class TraceService : TraceServiceBase
    {
        private readonly TelemetryStorage _storage;

        public TraceService(TelemetryStorage storage)
        {
            _storage = storage;
        }

        public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
        {
            var addContext = new AddContext();

            _storage.AddTraces(addContext, request.ResourceSpans);

            return Task.FromResult(new ExportTraceServiceResponse
            {
                PartialSuccess = new ExportTracePartialSuccess
                {
                    RejectedSpans = addContext.FailureCount
                }
            });
        }

    }
}
