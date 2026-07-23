using System;
using Newtonsoft.Json;

namespace Ee4v.Core.Settings
{
    internal sealed class NewtonsoftSettingValueSerializer : ISettingValueSerializer
    {
        public string Serialize(Type valueType, object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.None);
        }

        public bool TryDeserialize(Type valueType, string serializedValue, out object value)
        {
            if (string.IsNullOrWhiteSpace(serializedValue))
            {
                value = null;
                return false;
            }

            try
            {
                value = JsonConvert.DeserializeObject(serializedValue, valueType);
                return true;
            }
            catch (JsonException)
            {
                value = null;
                return false;
            }
        }
    }
}
