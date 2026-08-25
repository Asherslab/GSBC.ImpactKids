using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

// Elvanto's create/edit endpoints require every optional/custom field (birthday, custom_{id}, etc.)
// to be nested under a top-level "fields" object rather than sent as sibling properties.
public class ElvantoPersonFields
{
    [JsonPropertyName("birthday")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Birthday { get; set; }

    // The option id of a "select" custom field, as a plain string. Elvanto's docs say Drop Down and
    // Checkbox fields must be arrays, but an array was refused here with "Invalid Value for custom
    // field" for both the option name and the option id - and this account has no checkbox or
    // multi-select field type at all, only select and radio, so the array rule appears to belong to
    // a multi-value type this field is not.
    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldMediaConsentId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaConsent { get; set; }

    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldFirstTimeId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstTimeAtImpactKids { get; set; }

    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldMedicalId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MedicalAllergyNotes { get; set; }
}
