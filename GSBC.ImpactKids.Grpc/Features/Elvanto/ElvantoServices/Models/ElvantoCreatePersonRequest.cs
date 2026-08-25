using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoCreatePersonRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/create.json");

    public static ElvantoMutation Mutation => ElvantoMutation.Create;

    [JsonPropertyName("firstname")]
    public required string FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public required string LastName { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [JsonPropertyName("mobile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mobile { get; set; }

    /// <summary>
    /// People category. Omitted falls back to the account default, which is not what a person the
    /// app has invented should land in - everyone created from here is a visitor until a human
    /// says otherwise.
    /// </summary>
    [JsonPropertyName("category_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CategoryId { get; set; }

    /// <summary>
    /// Existing Elvanto family id - the numeric one Elvanto reports (e.g. "4742"), not the app's
    /// local family Guid. Omitted rather than guessed when the family has no Elvanto presence:
    /// "new" would create a second family alongside one that may already exist under another name.
    /// </summary>
    [JsonPropertyName("family_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FamilyId { get; set; }

    /// <summary>
    /// One of Elvanto's accepted values: Primary Contact, Spouse, Partner, Child, Sibling,
    /// Grandfather, Grandmother, Other. Only meaningful alongside <see cref="FamilyId"/>.
    /// </summary>
    [JsonPropertyName("family_relationship")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FamilyRelationship { get; set; }

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
