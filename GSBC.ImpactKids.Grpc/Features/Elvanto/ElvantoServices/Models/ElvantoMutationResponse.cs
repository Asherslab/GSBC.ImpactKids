using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoMutationResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public ElvantoApiError? Error { get; set; }
}

public class ElvantoCreatePersonResponse : ElvantoMutationResponse
{
    [JsonPropertyName("person")]
    public ElvantoCreatedPerson? Person { get; set; }
}

public class ElvantoCreatedPerson
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class ElvantoApiError
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
