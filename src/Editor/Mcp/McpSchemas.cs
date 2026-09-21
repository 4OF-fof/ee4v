using System;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal static class McpSchemas
    {
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
                ["enum"] = new JArray(values ?? Array.Empty<string>())
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
