using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class McpSchemas
    {
        // Validate the JSON Schema keywords emitted by this catalog before any
        // adapter can turn missing fields into destructive default values.
        internal static void Validate(JObject schema, JToken value)
        {
            var error = ValidationError(schema, value, "arguments");
            if (error != null) throw new McpToolException("invalid_request", error);
        }

        private static string ValidationError(JObject schema, JToken value, string path)
        {
            var type = (string)schema["type"];
            var validType = type == null || value != null && (
                type == "object" && value.Type == JTokenType.Object ||
                type == "array" && value.Type == JTokenType.Array ||
                type == "string" && value.Type == JTokenType.String ||
                type == "boolean" && value.Type == JTokenType.Boolean ||
                type == "integer" && value.Type == JTokenType.Integer ||
                type == "number" && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float));
            if (!validType) return path + " must be " + type + ".";
            if (type == "number" && (double.IsNaN((double)value) || double.IsInfinity((double)value)))
                return path + " must be finite.";
            if (schema["enum"] is JArray choices && !choices.Any(choice => JToken.DeepEquals(choice, value)))
                return path + " must be one of " + choices.ToString(Newtonsoft.Json.Formatting.None) + ".";
            if (value is JObject obj)
            {
                if (schema["required"] is JArray required)
                    foreach (var field in required.Values<string>())
                        if (obj.Property(field) == null) return path + "." + field + " is required.";
                var properties = schema["properties"] as JObject;
                foreach (var field in obj.Properties())
                {
                    if (properties?[field.Name] is JObject propertySchema)
                    {
                        var error = ValidationError(propertySchema, field.Value, path + "." + field.Name);
                        if (error != null) return error;
                    }
                    else if ((bool?)schema["additionalProperties"] == false)
                        return path + "." + field.Name + " is not allowed.";
                }
            }
            if (value is JArray array)
            {
                if (schema["minItems"] != null && array.Count < (int)schema["minItems"])
                    return path + " requires at least " + schema["minItems"] + " items.";
                if (schema["items"] is JObject itemSchema)
                    for (var index = 0; index < array.Count; index++)
                    {
                        var error = ValidationError(itemSchema, array[index], path + "[" + index + "]");
                        if (error != null) return error;
                    }
            }
            foreach (var keyword in new[] { "oneOf", "anyOf" })
            {
                if (!(schema[keyword] is JArray alternatives)) continue;
                var errors = alternatives.Cast<JObject>().Select(branch => ValidationError(branch, value, path)).ToArray();
                var matches = errors.Count(error => error == null);
                if (matches == 0 || keyword == "oneOf" && matches != 1)
                    return path + " must match " + (keyword == "oneOf" ? "exactly one" : "at least one") +
                           " input variant. " + string.Join(" ", errors.Where(error => error != null));
            }
            return null;
        }

        internal static JObject Object(
            JObject properties = null,
            params string[] required)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties ?? new JObject(),
                ["additionalProperties"] = false
            };
            if (required != null && required.Length > 0)
            {
                schema["required"] = new JArray(required);
            }

            return schema;
        }

        internal static JObject String(string description = null)
        {
            return Property("string", description);
        }

        internal static JObject Boolean(string description = null)
        {
            return Property("boolean", description);
        }

        internal static JObject Integer(string description = null)
        {
            return Property("integer", description);
        }

        internal static JObject Number(string description = null)
        {
            return Property("number", description);
        }

        internal static JObject Enum(params string[] values)
        {
            return new JObject
            {
                ["type"] = "string",
                ["enum"] = new JArray(values ?? System.Array.Empty<string>())
            };
        }

        internal static JObject Array(JObject items, string description = null)
        {
            var schema = new JObject
            {
                ["type"] = "array",
                ["items"] = items ?? new JObject()
            };
            if (!string.IsNullOrEmpty(description))
            {
                schema["description"] = description;
            }

            return schema;
        }

        private static JObject Property(string type, string description)
        {
            var schema = new JObject { ["type"] = type };
            if (!string.IsNullOrEmpty(description))
            {
                schema["description"] = description;
            }

            return schema;
        }
    }
}
