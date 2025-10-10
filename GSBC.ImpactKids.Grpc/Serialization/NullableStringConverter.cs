using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Serialization;

public class NullableStringConverter<T> : JsonConverter<T?>
{
    private readonly byte[] _empty = [];

    // if the original json has a "" instead of an object, the object is null. 
    // dumb elvanto stuff.
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            return JsonSerializer.Deserialize<T>(ref reader, options);
        if (reader.ValueTextEquals(_empty))
            return default;
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}