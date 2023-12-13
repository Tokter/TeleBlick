using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TeleBlick.OpenTelemetry.Models;

namespace TeleBlick.OpenTelemetry
{
    public static class Extensions
    {
        public static string? GetServiceId(this Resource resource)
        {
            string? serviceName = null;

            for (var i = 0; i < resource.Attributes.Count; i++)
            {
                var attribute = resource.Attributes[i];

                switch (attribute.Key)
                {
                    case Application.SERVICE_INSTANCE_ID:
                        return attribute.Value.GetString();

                    case Application.SERVICE_NAME:
                        serviceName = attribute.Value.GetString();
                        break;
                }

            }
            return serviceName;
        }

        public static string ToHexString(this ByteString bytes)
        {
            return Convert.ToHexString(bytes.Span);
        }

        public static string GetString(this AnyValue value) =>
            value.ValueCase switch
            {
                AnyValue.ValueOneofCase.StringValue => value.StringValue,
                AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
                AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
                AnyValue.ValueOneofCase.BoolValue => value.BoolValue ? "true" : "false",
                AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToHexString(),
                _ => value.ToString(),
            };

        public static Dictionary<string,string> ToDictionary(this RepeatedField<KeyValue> attributes)
        {
            if (attributes.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            var values = new Dictionary<string, string>();
            foreach(var attribute in attributes)
            {
                values.Add(attribute.Key, attribute.Value.GetString());
            }

            return values;
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime ToDateTime(this ulong unixTimeNanoSeconds)
        {
            return UnixEpoch.AddTicks((long)(unixTimeNanoSeconds / 100));
        }

        public static string ConcatProperties(this KeyValuePair<string, string>[] properties)
        {
            StringBuilder sb = new();
            var first = true;
            foreach (var kv in properties)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append(CultureInfo.InvariantCulture, $"{kv.Key}: ");
                sb.Append(string.IsNullOrEmpty(kv.Value) ? "\'\'" : kv.Value);
            }
            return sb.ToString();
        }

        public static void CopyKeyValuePairs(this RepeatedField<KeyValue> attributes, [NotNull] ref KeyValuePair<string, string>[]? copiedAttributes)
        {
            if (copiedAttributes is null || copiedAttributes.Length < attributes.Count)
            {
                copiedAttributes = new KeyValuePair<string, string>[attributes.Count];
            }
            else
            {
                Array.Clear(copiedAttributes);
            }

            CopyKeyValues(attributes, copiedAttributes);
        }

        private static void CopyKeyValues(RepeatedField<KeyValue> attributes, KeyValuePair<string, string>[] copiedAttributes)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                var keyValue = attributes[i];
                copiedAttributes[i] = new KeyValuePair<string, string>(keyValue.Key, keyValue.Value.GetString());
            }
        }

        public static string? GetValue(this KeyValuePair<string, string>[] values, string name)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key == name)
                {
                    return values[i].Value;
                }
            }
            return null;
        }
    }
}
