using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleBlick.OpenTelemetry.Services;
using MS = OpenTelemetry.Proto.Collector.Metrics.V1.MetricsService;
using LS = OpenTelemetry.Proto.Collector.Logs.V1.LogsService;
using TS = OpenTelemetry.Proto.Collector.Trace.V1.TraceService;
using Grpc.Core.Interceptors;
using Grpc.Core.Logging;

namespace TeleBlick.OpenTelemetry
{
    public class TelemetryServer
    {
        private readonly Server _server;
        private readonly TelemetryStorage _storage;

        public TelemetryServer()
        {
            //Grpc.Core.GrpcEnvironment.SetLogger(new TestLogger());
            _storage = new TelemetryStorage();

            _server = new Server
            {
                Services =
                {
                    MS.BindService(new MetricsService(_storage)),
                    LS.BindService(new LogsService()),
                    TS.BindService(new TraceService(_storage)), //.Intercept(new Test()),
                },
                Ports =
                    {
                    new ServerPort("localhost", 55678, ServerCredentials.Insecure)
                }
            };
        }

            public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            _server.ShutdownAsync().GetAwaiter().GetResult();
        }
    }

    public class Test : Interceptor
    {
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            return base.AsyncUnaryCall(request, context, continuation);
        }
    }

    public class TestLogger : ILogger
    {
        public void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Debug: {message}");
        }

        public void Debug(string format, params object[] formatArgs)
        {
            System.Diagnostics.Debug.WriteLine($"Debug: {format}", formatArgs);
        }

        public void Error(string message)
        {
            throw new NotImplementedException();
        }

        public void Error(string format, params object[] formatArgs)
        {
            throw new NotImplementedException();
        }

        public void Error(Exception exception, string message)
        {
            throw new NotImplementedException();
        }

        public ILogger ForType<T>()
        {
            Console.WriteLine($"ForType: {typeof(T).Name}");
            return this;
        }

        public void Info(string message)
        {
            throw new NotImplementedException();
        }

        public void Info(string format, params object[] formatArgs)
        {
            throw new NotImplementedException();
        }

        public void Warning(string message)
        {
            throw new NotImplementedException();
        }

        public void Warning(string format, params object[] formatArgs)
        {
            throw new NotImplementedException();
        }

        public void Warning(Exception exception, string message)
        {
            throw new NotImplementedException();
        }
    }
}
