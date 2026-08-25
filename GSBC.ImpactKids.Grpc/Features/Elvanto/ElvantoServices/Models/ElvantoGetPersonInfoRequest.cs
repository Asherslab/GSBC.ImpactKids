using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoGetPersonInfoRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/getInfo.json");

    public static bool IsMutation => false;

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
    [JsonPropertyName("person")]
    public ElvantoPerson? Person { get; set; }
}
