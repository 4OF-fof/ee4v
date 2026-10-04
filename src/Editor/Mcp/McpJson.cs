using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Ee4v.Mcp
{
    internal static class McpJson
    {
        private static readonly JsonSerializer Serializer =
            JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include
            });

        internal static JToken From(object value)
        {
            return value == null
                ? JValue.CreateNull()
                : JToken.FromObject(value, Serializer);
        }

        internal static T To<T>(JToken value)
        {
            return value == null ? default : value.ToObject<T>(Serializer);
        }

        internal static string RequireString(JObject arguments, string name)
        {
            var value = ((string)arguments[name] ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                throw new McpToolException("invalid_request", name + " is required.");
            }

            return value;
        }
    }
}
