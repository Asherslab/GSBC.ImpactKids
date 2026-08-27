using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoGetPersonInfoRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/getInfo.json");

    public static ElvantoMutation Mutation => ElvantoMutation.None;

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fields")]
    public string[] Fields { get; set; } =
    [
        "school_grade",
        "birthday",
        $"custom_{ElvantoPerson.CustomFieldMedicalId}",
        $"custom_{ElvantoPerson.CustomFieldMediaConsentId}",
        $"custom_{ElvantoPerson.CustomFieldFirstTimeId}"
    ];
}

public class ElvantoGetPersonInfoResponse
{
    /// <summary>
    /// A <b>list</b>, because that is what Elvanto sends: <c>people/getInfo</c> answers with
    /// <c>"person": [ { ... } ]</c> even for a single id, exactly as <c>people/getAll</c> does.
    ///
    /// This was declared as a single <c>ElvantoPerson?</c>, and the mismatch made every call return
    /// null. System.Text.Json threw on <c>$.person</c>, the transport's catch-all logged a warning
    /// and returned default, and the caller read that as "Elvanto has no such person" off a clean
    /// HTTP 200 — a parse failure wearing the costume of an empty result. It cost the family
    /// read-back on <c>people/edit</c>, which is the only source of a newly minted household's id,
    /// and it is why the person- and family-scoped syncs silently processed nobody before they were
    /// removed.
    /// </summary>
    [JsonPropertyName("person")]
    public List<ElvantoPerson>? Person { get; set; }
}
