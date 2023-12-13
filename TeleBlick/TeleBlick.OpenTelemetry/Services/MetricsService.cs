using Grpc.Core;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTelemetry.Proto.Collector.Metrics.V1.MetricsService;

namespace TeleBlick.OpenTelemetry.Services
{
    public class MetricsService : MetricsServiceBase
    {
        private readonly TelemetryStorage _storage;

        public MetricsService(TelemetryStorage storage)
        {
            _storage = storage;
        }

        public override Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context)
        {
            var addContext = new AddContext();
            _storage.AddMetrics(addContext, request.ResourceMetrics);

            return Task.FromResult(new ExportMetricsServiceResponse
            {
                PartialSuccess = new ExportMetricsPartialSuccess
                {
                    RejectedDataPoints = addContext.FailureCount
                }
            });
        }
    }
}
