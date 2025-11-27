using System;
using _4OF.ee4v.Core.i18n;
using Newtonsoft.Json;

namespace _4OF.ee4v.Core.Utility {
    public class UlidJsonConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(Ulid) || objectType == typeof(Ulid?);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.Null)
                return objectType == typeof(Ulid?)
                    ? null
                    : throw new JsonSerializationException(I18N.Get("Debug.Core.Utility.UlidJsonConverter.CannotConvertNull"));

            if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException(
                    I18N.Get("Debug.Core.Utility.UlidJsonConverter.UnexpectedTokenFmt", reader.TokenType));

            var str = (string)reader.Value;
            return Ulid.TryParse(str, out var ulid)
                ? ulid
                : throw new JsonSerializationException(I18N.Get("Debug.Core.Utility.Ulid.InvalidString"));
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