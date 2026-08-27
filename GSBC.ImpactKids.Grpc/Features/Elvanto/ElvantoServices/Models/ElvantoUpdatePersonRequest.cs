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

    /// <summary>
    /// One of Elvanto's accepted values: Primary Contact, Spouse, Partner, Child, Sibling,
    /// Grandfather, Grandmother, Other.
    ///
    /// Only ever set to "Primary Contact", and only by FamilyGuardianDescriptor promoting someone
    /// Elvanto does not currently treat as a guardian. The app holds one boolean where Elvanto holds
    /// eight relationships, so it has nothing to say about which of them a non-guardian should be -
    /// see the descriptor for why the reverse is refused rather than guessed at.
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

    /// <summary>
    /// Elvanto's school grade id — the same id <c>people/getAll</c> returns under
    /// <c>school_grade.id</c>, which is what <c>DbSchoolGrade.ElvantoId</c> stores. A standard
    /// optional people field, so it travels under <c>fields</c> like the birthday rather than at the
    /// top level, where it is rejected as a param that does not exist.
    ///
    /// Only ever a grade the app can name in Elvanto's terms. A local grade row with no
    /// <c>ElvantoId</c>, and a child with no grade at all, both arrive here as null and are declined
    /// rather than turned into a clear — see <c>SchoolGradeDescriptor</c>. There is no clear to send
    /// in any case: an empty string answers with a 500 rather than emptying the field.
    /// </summary>
    [JsonIgnore]
    public string? SchoolGrade
    {
        get => Fields?.SchoolGrade;
        set => (Fields ??= new ElvantoPersonFields()).SchoolGrade = value;
    }

    [JsonIgnore]
    public string? MedicalAllergyNotes
    {
        get => Fields?.MedicalAllergyNotes;
        set => (Fields ??= new ElvantoPersonFields()).MedicalAllergyNotes = value;
    }
}
