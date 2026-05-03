using System;
using System.Collections.Generic;
using System.Text.Json;
using TclInterpreter.Typing;

namespace TclInterpreter.Json
{
    /// <summary>
    /// Bridges between JSON and DSL values.
    /// </summary>
    public class JsonBridge
    {
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
                JsonValueKind.Null => new ObjectValue(), // Empty object for null
                _ => throw new JsonBridgeException($"Unsupported JSON value kind: {element.ValueKind}")
            };
        }

        public static JsonElement ToJson(DslValue value)
        {
            using var doc = JsonDocument.Parse(SerializeToJsonString(value));
            return doc.RootElement.Clone();
        }

        private static string SerializeToJsonString(DslValue value)
        {
            return value switch
            {
                NumericValue nv => nv.Value.ToString("G29"), // Preserve precision
                BoolValue bv => bv.Value ? "true" : "false",
                StringValue sv => JsonSerializer.Serialize(sv.Value),
                ObjectValue ov => SerializeObject(ov),
                _ => throw new JsonBridgeException($"Unknown DSL value type: {value.GetType().Name}")
            };
        }

        private static string SerializeObject(ObjectValue ov)
        {
            var dict = new Dictionary<string, object>();

            foreach (var (key, value) in ov.Fields)
            {
                if (key is not (long or int or string))
                    throw new JsonBridgeException($"Object keys may only be strings or numbers, got: {key.GetType().Name}");

                var keyStr = key.ToString() ?? "";
                dict[keyStr] = SerializeToJsonString(value);
            }

            return JsonSerializer.Serialize(dict);
        }

        private static DslValue ConvertNumber(JsonElement element)
        {
            if (element.TryGetInt64(out var longValue))
                return new NumericValue(longValue, "int");

            if (element.TryGetDecimal(out var decValue))
                return new NumericValue(decValue, "real");

            throw new JsonBridgeException("Could not convert JSON number");
        }

        private static DslValue ConvertObject(JsonElement element)
        {
            var result = new ObjectValue();

            foreach (var property in element.EnumerateObject())
            {
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
            var result = new ObjectValue();
            var index = 0;

            foreach (var item in element.EnumerateArray())
            {
                result.Fields[index] = FromJson(item);
                index++;
            }

            return result;
        }

        public static Dictionary<string, DslValue> DeserializeParameters(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonBridgeException("Input must be a JSON object");

            var parameters = new Dictionary<string, DslValue>();

            foreach (var property in root.EnumerateObject())
            {
                parameters[property.Name] = FromJson(property.Value);
            }

            return parameters;
        }

        public static string SerializeResult(DslValue result)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = SerializeToJsonString(result);
            
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, options);
        }
    }

    public class JsonBridgeException : Exception
    {
        public JsonBridgeException(string message) : base(message) { }
    }
}
