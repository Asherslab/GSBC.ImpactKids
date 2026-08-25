using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class PeopleRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/getAll.json");

    public static bool IsMutation => false;

    [JsonPropertyName("suspended")]
    public string? Suspended { get; set; }

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("archived")]
    public string? Archived { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("search")]
    public SearchObject? SearchObject { get; set; }

    [JsonPropertyName("fields")]
    public required string[] Fields { get; set; }
}

public class SearchObject
{
    [JsonPropertyName("school_grade")]
    public string? SchoolGrade { get; set; }
}