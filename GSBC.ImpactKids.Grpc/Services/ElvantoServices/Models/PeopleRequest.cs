using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;

public class PeopleRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/people/search.json");

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("search")]
    public SearchObject? SearchObject { get; set; }
    
    [JsonPropertyName("fields")]
    public string[] Fields { get; set; }
}

public class SearchObject
{
    [JsonPropertyName("school_grade")]
    public string? SchoolGrade { get; set; }
}