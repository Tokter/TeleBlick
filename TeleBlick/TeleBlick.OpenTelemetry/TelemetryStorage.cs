using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleBlick.OpenTelemetry.Models;
using System.Diagnostics.CodeAnalysis;
using Span = OpenTelemetry.Proto.Trace.V1.Span;
using ModelSpan = TeleBlick.OpenTelemetry.Models.Span;
using SpanKind = OpenTelemetry.Proto.Trace.V1.Span.Types.SpanKind;
using ModelSpanKind = TeleBlick.OpenTelemetry.Models.SpanKind;
using OpenTelemetry.Proto.Metrics.V1;

namespace TeleBlick.OpenTelemetry
{
    public class TelemetryStorage
    {
        private readonly ConcurrentDictionary<string, Application> _applications = new();
        private readonly CircularBuffer<Trace> _traces;

        public TelemetryStorage()
        {
            //_logs = new(config.GetValue("MaxLogCount", DefaultMaxTelemetryCount));
            _traces = new(10000); // new(config.GetValue("MaxTraceCount", 10000));
        }

        public Application GetOrAddApplication(Resource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);

            var serviceInstanceId = resource.GetServiceId();

            if (serviceInstanceId is null)
            {
                throw new InvalidOperationException($"Resource does not have a '{Application.SERVICE_INSTANCE_ID}' attribute.");
            }

            if (_applications.TryGetValue(serviceInstanceId, out var application))
            {
                return application;
            }

            return _applications.GetOrAdd(serviceInstanceId, _ => { return new Application(resource); });
        }

        public void AddMetrics(AddContext context, RepeatedField<ResourceMetrics> resourceMetrics)
        {
            foreach (var rm in resourceMetrics)
            {
                Application application;
                try
                {
                    application = GetOrAddApplication(rm.Resource);
                }
                catch (Exception)
                {
                    context.FailureCount += rm.ScopeMetrics.Sum(s => s.Metrics.Count);
                    continue;
                }

                application.AddMetrics(context, rm.ScopeMetrics);
            }
        }

        public void AddTraces(AddContext context, RepeatedField<ResourceSpans> resourceSpans)
        {
            foreach (var rs in resourceSpans)
            {
                Application application;
                try
                {
                    application = GetOrAddApplication(rs.Resource);
                }
                catch (Exception)
                {
                    context.FailureCount += rs.ScopeSpans.Sum(s => s.Spans.Count);
                    continue;
                }

                AddTraces(context, application, rs.ScopeSpans);
            }
        }

        private readonly ReaderWriterLockSlim _tracesLock = new();
        private readonly Dictionary<string, Scope> _traceScopes = new();
        private void AddTraces(AddContext context, Application application, RepeatedField<ScopeSpans> scopeSpans)
        {
            _tracesLock.EnterWriteLock();

            try
            {
                foreach (var scopeSpan in scopeSpans)
                {
                    Scope? scope;
                    try
                    {
                        // The instrumentation scope information for the spans in this message.
                        // Semantically when InstrumentationScope isn't set, it is equivalent with
                        // an empty instrumentation scope name (unknown).
                        var name = scopeSpan.Scope?.Name ?? string.Empty;
                        if (!_traceScopes.TryGetValue(name, out scope))
                        {
                            scope = (scopeSpan.Scope != null) ? new Scope(scopeSpan.Scope) : Scope.Empty;
                            _traceScopes.Add(name, scope);
                        }
                    }
                    catch (Exception)
                    {
                        context.FailureCount += scopeSpan.Spans.Count;
                        continue;
                    }

                    Trace? lastTrace = null;

                    foreach (var span in scopeSpan.Spans)
                    {
                        try
                        {
                            Trace? trace;
                            bool newTrace = false;

                            // Fast path to check if the span is in the same trace as the last span.
                            if (lastTrace != null && span.TraceId.Span.SequenceEqual(lastTrace.Key.Span))
                            {
                                trace = lastTrace;
                            }
                            else if (!TryGetTraceById(_traces, span.TraceId.Memory, out trace))
                            {
                                trace = new Trace(span.TraceId.Memory, scope);
                                newTrace = true;
                            }

                            var newSpan = CreateSpan(application, span, trace);
                            trace.AddSpan(newSpan);

                            // Traces are sorted by the start time of the first span.
                            // We need to ensure traces are in the correct order if we're:
                            // 1. Adding a new trace.
                            // 2. The first span of the trace has changed.
                            if (newTrace)
                            {
                                var added = false;
                                for (var i = _traces.Count - 1; i >= 0; i--)
                                {
                                    var currentTrace = _traces[i];
                                    if (trace.FirstSpan.StartTime > currentTrace.FirstSpan.StartTime)
                                    {
                                        _traces.Insert(i + 1, trace);
                                        added = true;
                                        break;
                                    }
                                }
                                if (!added)
                                {
                                    _traces.Insert(0, trace);
                                }
                            }
                            else
                            {
                                if (trace.FirstSpan == newSpan)
                                {
                                    var moved = false;
                                    var index = _traces.IndexOf(trace);

                                    for (var i = index - 1; i >= 0; i--)
                                    {
                                        var currentTrace = _traces[i];
                                        if (trace.FirstSpan.StartTime > currentTrace.FirstSpan.StartTime)
                                        {
                                            var insertPosition = i + 1;
                                            if (index != insertPosition)
                                            {
                                                _traces.RemoveAt(index);
                                                _traces.Insert(insertPosition, trace);
                                            }
                                            moved = true;
                                            break;
                                        }
                                    }
                                    if (!moved)
                                    {
                                        if (index != 0)
                                        {
                                            _traces.RemoveAt(index);
                                            _traces.Insert(0, trace);
                                        }
                                    }
                                }
                            }

                            lastTrace = trace;
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
                _tracesLock.ExitWriteLock();
            }

            static bool TryGetTraceById(CircularBuffer<Trace> traces, ReadOnlyMemory<byte> traceId, [NotNullWhen(true)] out Trace? trace)
            {
                var s = traceId.Span;
                for (var i = traces.Count - 1; i >= 0; i--)
                {
                    if (traces[i].Key.Span.SequenceEqual(s))
                    {
                        trace = traces[i];
                        return true;
                    }
                }

                trace = null;
                return false;
            }
        }

        private static ModelSpan CreateSpan(Application application, Span span, Trace trace)
        {
            var id = span.SpanId?.ToHexString();
            if (id is null)
            {
                throw new ArgumentException("Span has no SpanId");
            }

            var events = new List<SpanEvent>();
            foreach (var e in span.Events)
            {
                events.Add(new SpanEvent()
                {
                    Name = e.Name,
                    Time = e.TimeUnixNano.ToDateTime(),
                    Attributes = e.Attributes.ToDictionary()
                });
            }

            var newSpan = new ModelSpan(application, trace)
            {
                SpanId = id,
                ParentSpanId = span.ParentSpanId?.ToHexString(),
                Name = span.Name,
                Kind = ConvertSpanKind(span.Kind),
                StartTime = span.StartTimeUnixNano.ToDateTime(),
                EndTime = span.EndTimeUnixNano.ToDateTime(),
                Status = ConvertStatus(span.Status),
                StatusMessage = span.Status?.Message,
                Attributes = span.Attributes.ToDictionary(),
                State = span.TraceState,
                Events = events
            };
            return newSpan;
        }

        private static ModelSpanKind ConvertSpanKind(SpanKind? kind)
        {
            return kind switch
            {
                SpanKind.Unspecified => ModelSpanKind.Internal,
                SpanKind.Internal => ModelSpanKind.Internal,
                SpanKind.Client => ModelSpanKind.Client,
                SpanKind.Server => ModelSpanKind.Server,
                SpanKind.Producer => ModelSpanKind.Producer,
                SpanKind.Consumer => ModelSpanKind.Consumer,
                _ => ModelSpanKind.Internal
            };
        }

        private static SpanStatus ConvertStatus(Status? status)
        {
            return status?.Code switch
            {
                Status.Types.StatusCode.Ok => SpanStatus.Ok,
                Status.Types.StatusCode.Error => SpanStatus.Error,
                Status.Types.StatusCode.Unset => SpanStatus.Unset,
                _ => SpanStatus.Unset
            };
        }

    }

    public class AddContext
    {
        public int FailureCount { get; set; }
    }
}
