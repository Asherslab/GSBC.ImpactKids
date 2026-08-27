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

    /// <summary>
    /// The family the new person landed in. Elvanto returns this as a number, and it is the only
    /// way to learn the id of a family created by passing <c>family_id: "new"</c> - without it the
    /// next sibling would ask for "new" again and end up in a second family of their own.
    /// </summary>
    [JsonPropertyName("family_id")]
    public long? FamilyId { get; set; }
}

public class ElvantoApiError
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
