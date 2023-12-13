using OpenTelemetry.Proto.Common.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleBlick.OpenTelemetry.Models
{
    /// <summary>
    /// The Instrumentation Scope represents a logical unit within the application code with which the emitted telemetry can be associated.
    /// It is typically the developer’s choice to decide what denotes a reasonable instrumentation scope, e.g.a module, a package or a class
    /// can be chosen as instrumentation scope.In the case of a library or framework, it is a common approach to use an identifier as scope
    /// that is unique to the library or framework, such as a fully qualified name and version of the library or framework.If the library
    /// itself does not have built-in OpenTelemetry instrumentation, and an Instrumentation Library is used instead, it is recommended to
    /// use the name and version of the Instrumentation Library as the instrumentation scope.
    /// The Instrumentation Scope is defined by a name and version pair when a Tracer, Meter or Logger instance is obtained from a provider.
    /// Each span, metric or log record created by the instance will be associated with the provided Instrumentation Scope.
    /// In your observability backend, this allows you to slice and dice your telemetry data by the Instrumentation Scope, e.g.to see which
    /// of your users are using which version of a library and what the performance of that library version is or to pin point a problem to a specific module of your application.
    /// https://opentelemetry.io/docs/concepts/instrumentation-scope/
    /// </summary>
    public class Scope
    {
        public static readonly Scope Empty = new Scope();
        public string ScopeName { get; }
        public string Version { get; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        private Scope()
        {
            ScopeName = string.Empty;
            Version = string.Empty;
        }

        public Scope(InstrumentationScope scope)
        {
            ScopeName = scope.Name;
            Version = scope.Version;
            Properties = scope.Attributes.ToDictionary();
        }
    }
}
