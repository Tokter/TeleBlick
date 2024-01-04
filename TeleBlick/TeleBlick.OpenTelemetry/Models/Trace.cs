using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    /// <summary>
    /// Traces give us the big picture of what happens when a request is made to an application. Whether your application
    /// is a monolith with a single database or a sophisticated mesh of services, traces are essential to
    /// understanding the full “path” a request takes in your application.
    /// https://opentelemetry.io/docs/concepts/signals/traces/
    /// </summary>
    [DebuggerDisplay("{DebuggerToString(),nq}")]
    public class Trace
    {
        private Span? _rootSpan;

        public ReadOnlyMemory<byte> Key { get; }
        public string TraceId { get; }
        public string FullName { get; private set; }
        public Span FirstSpan => Spans[0];
        public Span? RootSpan => _rootSpan;
        public TimeSpan Duration
        {
            get
            {
                var start = FirstSpan.StartTime;
                DateTime end = default;
                foreach (var span in Spans)
                {
                    if (span.EndTime > end)
                    {
                        end = span.EndTime;
                    }
                }
                return end - start;
            }
        }

        public List<Span> Spans { get; } = new List<Span>();
        public Scope TraceScope { get; }

        public Trace(ReadOnlyMemory<byte> traceId, Scope traceScope)
        {
            Key = traceId;
            TraceId =  Convert.ToHexString(traceId.Span);
            TraceScope = traceScope;
            FullName = string.Empty;
        }

        public Trace(TelemetryStorage storage, BinaryReader reader)
        {
            TraceId = reader.ReadString();
            Key = Convert.FromHexString(TraceId);
            FullName = reader.ReadString();

            if (reader.ReadBoolean())
            {
                var scopeName = reader.ReadString();
                TraceScope = storage.GetScope(scopeName);
            }
            else
            {
                TraceScope = Scope.Empty;
            }

            var spanCount = reader.ReadInt32();
            for (var i = 0; i < spanCount; i++)
            {
                var span = new Span(storage, this, reader);
                Spans.Add(span);
                if (string.IsNullOrEmpty(span.ParentSpanId))
                {
                    _rootSpan = span;
                }
            }
        }

        public int CalculateDepth(Span span)
        {
            var depth = 0;
            var currentSpan = span;
            while (currentSpan != null)
            {
                depth++;
                currentSpan = currentSpan.GetParentSpan();
            }
            return depth;
        }

        public int CalculateMaxDepth() => Spans.Max(CalculateDepth);

        public void AddSpan(Span span)
        {
            var added = false;
            for (var i = Spans.Count - 1; i >= 0; i--)
            {
                if (span.StartTime > Spans[i].StartTime)
                {
                    Spans.Insert(i + 1, span);
                    added = true;
                    break;
                }
            }
            if (!added)
            {
                Spans.Insert(0, span);
                FullName = $"{span.Source.ApplicationName}: {span.Name}";
            }

            if (string.IsNullOrEmpty(span.ParentSpanId))
            {
                _rootSpan = span;
            }
        }

        private string DebuggerToString()
        {
            return $@"TraceId = ""{TraceId}"", Spans = {Spans.Count}, StartTime = {FirstSpan.StartTime.ToLocalTime():h:mm:ss.fff tt}, Duration = {Duration}";
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(TraceId);
            writer.Write(FullName);

            if (TraceScope != null && TraceScope != Scope.Empty)
            {
                writer.Write(true);
                writer.Write(TraceScope.ScopeName);
            }
            else
            {
                writer.Write(false);
            }
            writer.Write(Spans.Count);
            foreach (var span in Spans)
            {
                span.Serialize(writer);
            }
        }
    }
}
