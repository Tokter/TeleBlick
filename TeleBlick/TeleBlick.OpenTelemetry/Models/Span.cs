using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    public enum SpanKind
    {
        /// <summary>
        /// Default value. Indicates that the span represents an internal operation within an application, as opposed to an operations with remote parents or children.
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Indicates that the span covers server-side handling of a synchronous RPC or other remote request. This span is often the child of a remote CLIENT span that was expected to wait for a response.
        /// </summary>
        Server = 2,

        /// <summary>
        /// Indicates that the span describes a request to some remote service. This span is usually the parent of a remote SERVER span and does not end until the response is received.
        /// </summary>
        Client = 3,

        /// <summary>
        /// Indicates that the span describes the initiators of an asynchronous request. This parent span will often end before the corresponding child CONSUMER span, possibly even before the child span starts. In messaging scenarios with batching, tracing individual messages requires a new PRODUCER span per message to be created.
        /// </summary>
        Producer = 4,

        /// <summary>
        /// Indicates that the span describes a child of an asynchronous PRODUCER request.
        /// </summary>
        Consumer = 5,
    }

    /// <summary>
    /// A status will be attached to a span. Typically, you will set a span status when there is a known error in the application code, such as an exception. 
    /// A Span Status will be tagged as one of the following values:
    /// </summary>
    public enum SpanStatus
    {
        /// <summary>
        /// Default, by setting a Span status to Unset, the back-end that processes spans can now assign a final status.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// The Span has completed successfully.
        /// </summary>
        Ok = 1,

        /// <summary>
        /// When an exception is handled, a Span status can be set to Error
        /// </summary>
        Error = 2,
    }

    /// <summary>
    /// A span represents a unit of work or operation. Spans are the building blocks of Traces. In OpenTelemetry, they include the following information:
    /// - Name
    /// - Parent span ID(empty for root spans)
    /// - Start and End Timestamps
    /// - Span Context https://opentelemetry.io/docs/concepts/signals/traces/#span-context
    /// - Attributes https://opentelemetry.io/docs/concepts/signals/traces/#attributes
    /// - Span Events https://opentelemetry.io/docs/concepts/signals/traces/#span-events
    /// - Span Links https://opentelemetry.io/docs/concepts/signals/traces/#span-links
    /// - Span Status https://opentelemetry.io/docs/concepts/signals/traces/#span-status
    /// - Span Kind https://opentelemetry.io/docs/concepts/signals/traces/#span-kind
    /// </summary>
    [DebuggerDisplay("{DebuggerToString(),nq}")]
    public class Span
    {
        public const string PeerServiceAttributeKey = "peer.service";
        public const string SpanKindAttributeKey = "span.kind";

        public string TraceId => Trace.TraceId;
        public Trace Trace { get; }
        public Application Source { get; }

        public string SpanId { get; private set; }
        public string? ParentSpanId { get; private set; }
        public string Name { get; private set; }
        public SpanKind Kind { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public SpanStatus Status { get; private set; }
        public string? StatusMessage { get; private set; }
        public string? State { get; private set; }
        public Dictionary<string,string> Attributes { get; private set; }
        public List<SpanEvent> Events { get; private set; }

        public string ScopeName => Trace.TraceScope.ScopeName;
        public string ScopeSource => Source.ApplicationName;
        public TimeSpan Duration => EndTime - StartTime;

        public IEnumerable<Span> GetChildSpans() => Trace.Spans.Where(s => s.ParentSpanId == SpanId);
        public Span? GetParentSpan() => string.IsNullOrEmpty(ParentSpanId) ? null : Trace.Spans.Where(s => s.SpanId == ParentSpanId).FirstOrDefault();

        public Span(Application application, Trace trace, string spanId, string? parentSpanId, string name, SpanKind kind, DateTime startTime, DateTime endTime, SpanStatus status, string? statusMessage, string? state, Dictionary<string, string> attributes, List<SpanEvent> events)
        {
            Source = application;
            Trace = trace;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            Name = name;
            Kind = kind;
            StartTime = startTime;
            EndTime = endTime;
            Status = status;
            StatusMessage = statusMessage;
            State = state;
            Attributes = attributes;
            Events = events;
        }

        public Span(TelemetryStorage storage, Trace trace, BinaryReader reader)
        {
            Source = storage.GetApplication(reader.ReadString())!;
            Trace = trace;
            SpanId = reader.ReadString();
            if (reader.ReadBoolean())
            {
                ParentSpanId = reader.ReadString();
            }
            Name = reader.ReadString();
            Kind = (SpanKind)reader.ReadInt32();
            StartTime = DateTime.FromBinary(reader.ReadInt64());
            EndTime = DateTime.FromBinary(reader.ReadInt64());
            Status = (SpanStatus)reader.ReadInt32();
            if (reader.ReadBoolean())
            {
                StatusMessage = reader.ReadString();
            }
            if (reader.ReadBoolean())
            {
                State = reader.ReadString();
            }
            var attributeCount = reader.ReadInt32();
            Attributes = new Dictionary<string, string>(attributeCount);
            for (var i = 0; i < attributeCount; i++)
            {
                Attributes.Add(reader.ReadString(), reader.ReadString());
            }
            var eventCount = reader.ReadInt32();
            Events = new List<SpanEvent>(eventCount);
            for (var i = 0; i < eventCount; i++)
            {
                Events.Add(new SpanEvent(reader));
            }
        }

        private string DebuggerToString()
        {
            return $@"SpanId = {SpanId}, StartTime = {StartTime.ToLocalTime():h:mm:ss.fff tt}, ParentSpanId = {ParentSpanId}, TraceId = {Trace.TraceId}";
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Source.ApplicationName);
            writer.Write(SpanId);
            if (ParentSpanId != null)
            {
                writer.Write(true);
                writer.Write(ParentSpanId);
            }
            else
            {
                writer.Write(false);
            }
            writer.Write(Name);
            writer.Write((int)Kind);
            writer.Write(StartTime.ToBinary());
            writer.Write(EndTime.ToBinary());
            writer.Write((int)Status);
            if (StatusMessage != null)
            {
                writer.Write(true);
                writer.Write(StatusMessage);
            }
            else
            {
                writer.Write(false);
            }
            if (State != null)
            {
                writer.Write(true);
                writer.Write(State);
            }
            else
            {
                writer.Write(false);
            }
            writer.Write(Attributes.Count);
            foreach (var attribute in Attributes)
            {
                writer.Write(attribute.Key);
                writer.Write(attribute.Value);
            }
            writer.Write(Events.Count);
            foreach (var spanEvent in Events)
            {
                spanEvent.Write(writer);
            }
        }
    }
}
