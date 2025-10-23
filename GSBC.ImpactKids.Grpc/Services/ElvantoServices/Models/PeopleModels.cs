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
    [JsonPropertyName("person")]
    public Person[]? Person { get; set; }
}
