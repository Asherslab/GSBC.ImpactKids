using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoUpdatePersonRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/edit.json");

    public static ElvantoMutation Mutation => ElvantoMutation.Update;

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

    /// <summary>
    /// Elvanto's numeric family id. Only sent when a family move actually wins the last-write-wins
    /// comparison - a family_id on every edit would keep re-asserting a grouping nobody changed.
    /// </summary>
    [JsonPropertyName("family_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FamilyId { get; set; }

    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ElvantoPersonFields? Fields { get; set; }

    [JsonIgnore]
    public string? Birthday
    {
        get => Fields?.Birthday;
        set => (Fields ??= new ElvantoPersonFields()).Birthday = value;
    }

    /// <summary>
    /// Set with the display name ("Yes"); stored as the option id Elvanto requires for a select
    /// custom field, in the array that a select or checkbox field must be given. Reading it back
    /// returns the name again, so a caller never has to know about the ids.
    /// </summary>
    [JsonIgnore]
    public string? MediaConsent
    {
        get => Fields?.MediaConsent is { } id
                   ? MediaConsentOptions.NameForId(id) ?? id
                   : null;
        set => (Fields ??= new ElvantoPersonFields()).MediaConsent =
            value is null ? null : MediaConsentOptions.IdForName(value);
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
