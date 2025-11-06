using System.Text.Json.Serialization;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedMember.Global

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;

public class PeopleResponse
{
    [JsonPropertyName("people")]
    public People? People { get; set; }
}

public class People
{
    [JsonPropertyName("on_this_page")]
    public int OnThisPage { get; set; }
    
    [JsonPropertyName("page")]
    public int Page { get; set; }
    
    [JsonPropertyName("per_page")]
    public string? PerPage { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("person")]
    public ElvantoPerson[]? Person { get; set; }
}
