using System;
using Newtonsoft.Json;

namespace _4OF.ee4v.Core.Utility {
    public class UlidJsonConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(Ulid) || objectType == typeof(Ulid?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.Null) {
                return objectType == typeof(Ulid?) ? null : throw new JsonSerializationException("Cannot convert null value to Ulid.");
            }

            if (reader.TokenType != JsonToken.String) {
                throw new JsonSerializationException($"Unexpected token parsing Ulid. Expected String, got {reader.TokenType}.");
            }

            var str = (string)reader.Value;
            return Ulid.TryParse(str, out var ulid) ? ulid : throw new JsonSerializationException("Invalid ULID string.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (value == null) {
                writer.WriteNull();
                return;
            }

            var ulid = (Ulid)value;
            writer.WriteValue(ulid.ToString());
        }
    }
}
