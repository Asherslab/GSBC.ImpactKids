using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Serialization;

/// <summary>
/// Reads a value Elvanto sends as a string on some days and a bare number on others, keeping the
/// number's exact text. Without it a single numeric <c>family_id</c> throws on deserialize, the
/// whole getAll page comes back null, and every sync refuses to run.
/// </summary>
public class StringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null   => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
            _ => throw new JsonException($"Expected a string or a number, got {reader.TokenType}.")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
