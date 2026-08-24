using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

// Elvanto's create/edit endpoints require every optional/custom field (birthday, custom_{id}, etc.)
// to be nested under a top-level "fields" object rather than sent as sibling properties.
public class ElvantoPersonFields
{
    [JsonPropertyName("birthday")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Birthday { get; set; }

    // Elvanto requires Drop Down/Checkbox custom fields to be sent as an array, even for a single value.
    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldMediaConsentId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MediaConsent { get; set; }

    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldFirstTimeId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstTimeAtImpactKids { get; set; }

    [JsonPropertyName($"custom_{ElvantoPerson.CustomFieldMedicalId}")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MedicalAllergyNotes { get; set; }
}
