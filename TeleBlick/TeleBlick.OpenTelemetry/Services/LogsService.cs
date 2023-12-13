using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTelemetry.Proto.Collector.Logs.V1.LogsService;

namespace TeleBlick.OpenTelemetry.Services
{
    public class LogsService : LogsServiceBase
    {
        public override Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context)
        {
            return base.Export(request, context);
        }
    }
}
