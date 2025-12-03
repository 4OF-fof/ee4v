using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace _4OF.ee4v.AssetManager.Core {
    public class MetadataSerializer {
        private readonly JsonSerializerSettings _settings;

        public MetadataSerializer(IEnumerable<Type> allowedTypes = null) {
            _settings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new AllowedTypesBinder(allowedTypes)
            };
        }

        public string Serialize<T>(T obj) {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        public T Deserialize<T>(string json) {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        public object Deserialize(Type type, string json) {
            return JsonConvert.DeserializeObject(json, type, _settings);
        }
    }
}