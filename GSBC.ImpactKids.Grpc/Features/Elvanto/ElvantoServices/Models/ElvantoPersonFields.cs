using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

// Elvanto's create/edit endpoints require every optional/custom field (birthday, custom_{id}, etc.)
// to be nested under a top-level "fields" object rather than sent as sibling properties.
public class ElvantoPersonFields
{
    [JsonPropertyName("birthday")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Birthday { get; set; }

    /// <summary>
    /// "Male" or "Female". A standard people field rather than a custom one, but like the birthday
    /// and the school grade it is writable only under <c>fields</c>, never at the top level.
    /// </summary>
    [JsonPropertyName("gender")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Gender { get; set; }

    /// <summary>
    /// A standard optional people field, not a custom one, so it is named plainly rather than
    /// <c>custom_&lt;id&gt;</c> - but it still lives under <c>fields</c>. Sent at the top level it is
    /// rejected outright: <c>A param does not exist (school_grade)</c>.
    ///
    /// <b>The value is the grade id, not its name</b>, despite the docs describing it as "the name of
    /// the school grade". The name form works only for grades whose name is not numeric: this
    /// account's grades are named <c>1</c>-<c>12</c> plus Prep, Kindergarten and Nursery/Pre-school,
    /// and sending <c>"7"</c> answers with a 500 "problem when saving to the database" while sending
    /// that grade's id succeeds. Twelve of the fifteen would have been unpushable.
    /// </summary>
    [JsonPropertyName("school_grade")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SchoolGrade { get; set; }

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
