using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoUpdatePersonRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/edit.json");

    public static bool IsMutation => true;

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("firstname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [JsonPropertyName("mobile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mobile { get; set; }

    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElvantoPersonFields? Fields { get; set; }

    [JsonIgnore]
    public string? Birthday
    {
        get => Fields?.Birthday;
        set => (Fields ??= new ElvantoPersonFields()).Birthday = value;
    }

    [JsonIgnore]
    public string? MediaConsent
    {
        get => Fields?.MediaConsent?.FirstOrDefault();
        set => (Fields ??= new ElvantoPersonFields()).MediaConsent = value is null ? null : [value];
    }

    [JsonIgnore]
    public string? FirstTimeAtImpactKids
    {
        get => Fields?.FirstTimeAtImpactKids;
        set => (Fields ??= new ElvantoPersonFields()).FirstTimeAtImpactKids = value;
    }

    [JsonIgnore]
    public string? MedicalAllergyNotes
    {
        get => Fields?.MedicalAllergyNotes;
        set => (Fields ??= new ElvantoPersonFields()).MedicalAllergyNotes = value;
    }
}
