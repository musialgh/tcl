using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using TclInterpreter.Typing;

namespace TclInterpreter.Json
{
    public class JsonBridgeException : Exception
    {
        public JsonBridgeException(string message) : base(message) { }
    }

    /// <summary>
    /// Bridges between JSON and DSL values.
    /// </summary>
    public static class JsonBridge
    {
        // ================================================================
        //  JSON -> DslValue
        // ================================================================

        public static DslValue FromJson(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => ConvertNumber(element),
                JsonValueKind.String => new StringValue(element.GetString() ?? ""),
                JsonValueKind.True => new BoolValue(true),
                JsonValueKind.False => new BoolValue(false),
                JsonValueKind.Object => ConvertObject(element),
                JsonValueKind.Array => ConvertArray(element),
                JsonValueKind.Null => new ObjectValue(), // null -> empty object
                _ => throw new JsonBridgeException($"Unsupported JSON value kind: {element.ValueKind}")
            };
        }

        public static Dictionary<string, DslValue> DeserializeParameters(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonBridgeException("Input must be a JSON object");

            var parameters = new Dictionary<string, DslValue>();
            foreach (var property in root.EnumerateObject())
                parameters[property.Name] = FromJson(property.Value);

            return parameters;
        }

        private static DslValue ConvertNumber(JsonElement element)
        {
            // Try integer first
            if (element.TryGetInt64(out var longValue))
            {
                // Check if the raw text contains a decimal point
                var raw = element.GetRawText();
                if (!raw.Contains('.'))
                    return new NumericValue(longValue, "int");
            }
            if (element.TryGetDecimal(out var decValue))
                return new NumericValue(decValue, "real");

            throw new JsonBridgeException("Could not convert JSON number");
        }

        private static DslValue ConvertObject(JsonElement element)
        {
            var result = new ObjectValue();
            foreach (var property in element.EnumerateObject())
            {
                // Try to parse key as integer
                object key;
                if (long.TryParse(property.Name, out var numKey))
                    key = numKey;
                else
                    key = property.Name;

                result.Fields[key] = FromJson(property.Value);
            }
            return result;
        }

        private static DslValue ConvertArray(JsonElement element)
        {
            // Arrays become objects with 0-based integer keys
            var result = new ObjectValue();
            long index = 0;
            foreach (var item in element.EnumerateArray())
            {
                result.Fields[index] = FromJson(item);
                index++;
            }
            return result;
        }

        // ================================================================
        //  DslValue -> JSON
        // ================================================================

        public static string SerializeResult(DslValue result, bool indented = true)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = indented
            });

            WriteValue(writer, result);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteValue(Utf8JsonWriter writer, DslValue value)
        {
            switch (value)
            {
                case NumericValue nv:
                    if (nv.NumericType is "int" or "nat" && nv.Value == Math.Truncate(nv.Value))
                        writer.WriteNumberValue((long)nv.Value);
                    else
                        writer.WriteNumberValue(nv.Value);
                    break;

                case BoolValue bv:
                    writer.WriteBooleanValue(bv.Value);
                    break;

                case StringValue sv:
                    writer.WriteStringValue(sv.Value);
                    break;

                case ObjectValue ov:
                    WriteObject(writer, ov);
                    break;

                default:
                    throw new JsonBridgeException($"Cannot serialize DSL value of type {value.GetType().Name}");
            }
        }

        private static void WriteObject(Utf8JsonWriter writer, ObjectValue ov)
        {
            writer.WriteStartObject();

            foreach (var (key, value) in ov.Fields)
            {
                var keyStr = key switch
                {
                    long l => l.ToString(CultureInfo.InvariantCulture),
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    decimal d => d.ToString(CultureInfo.InvariantCulture),
                    string s => s,
                    ObjectValue o => QuoteObject((ObjectValue)key), // see below (hack)
                    _ => throw new JsonBridgeException(
                        $"Unexpected key type, got: {key.GetType().Name}")
                };

                writer.WritePropertyName(keyStr);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        private static string QuoteObject(ObjectValue ov)
        {
            // This is a tiny hack: JSON does not support object keys. We do not really need them.
            // But for debugging purposes without throwing exceptions, let's just quote the thingy as a string key.
            // Should even be usable as an input in many cases. But not always.
            return "_" + SerializeResult(ov, indented: false).Replace("\"", "") + "_";
        }
    }
}
