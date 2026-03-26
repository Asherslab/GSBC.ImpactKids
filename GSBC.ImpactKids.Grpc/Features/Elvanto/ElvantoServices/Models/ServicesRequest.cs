using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ServicesRequest : IRequestMessage
{
    public static Uri RequestUri { get; } = new("https://api.elvanto.com/v1/services/getAll.json");

    [JsonPropertyName("start")]
    public DateOnly Start { get; set; }

    [JsonPropertyName("end")]
    public DateOnly End { get; set; }

    [JsonPropertyName("service_types")]
    public required string[] ServiceTypes { get; set; }

    [JsonPropertyName("fields")]
    public required string[] Fields { get; set; }
}