using Newtonsoft.Json;

namespace NNostr.Client.JsonConverters
{
    public class UnixTimestampSecondsJsonConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? ReadJson(JsonReader reader, Type objectType, DateTimeOffset? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonToken.Integer)
            {
                throw new JsonException("datetime was not in number format");
            }

            return DateTimeOffset.FromUnixTimeSeconds((long)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, DateTimeOffset? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(value.Value.ToUnixTimeSeconds());
            }
        }
    }
}