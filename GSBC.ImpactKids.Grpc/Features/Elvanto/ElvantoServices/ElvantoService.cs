using System.Net.Http.Headers;
using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Elvanto;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class ElvantoService(
    GsbcDbContext           db,
    HttpClient              httpClient,
    ElvantoConfig           config,
    ILogger<ElvantoService> logger
) : IElvantoService
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Whether writes to Elvanto are permitted. Callers use this to report what they *would*
    /// have done instead of reporting a failure.
    /// </summary>
    public bool WritesEnabled => config.AllowWrites;

    /// <summary>
    /// The exact JSON body that would be POSTed for this request. Serialized with the same
    /// options JsonContent.Create uses, so what gets logged is what would go on the wire -
    /// null-valued fields omitted included.
    /// </summary>
    internal static string DescribePayload<TRequest>(TRequest request) =>
        System.Text.Json.JsonSerializer.Serialize(request);

    private async Task<TResponse?> SendMessage<TRequest, TResponse>(TRequest request, CancellationToken token = default)
        where TRequest : IRequestMessage
    {
        // The single gate every Elvanto call passes through. Callers are expected to check
        // WritesEnabled and log their own context first, but this is what actually makes a
        // push impossible: nothing below this line runs for a mutation while writes are off,
        // so a new caller that forgets the check still cannot reach the network.
        if (TRequest.IsMutation && !config.AllowWrites)
        {
            logger.LogWarning(
                "ELVANTO WRITE BLOCKED (Elvanto:AllowWrites=false). Nothing was sent to {Uri}. "
                + "Payload that would have been sent: {Payload}",
                TRequest.RequestUri, DescribePayload(request));
            return default;
        }

        HttpRequestMessage httpRequest = new(HttpMethod.Post, TRequest.RequestUri);
        string             encoded     = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.Authentication));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        httpRequest.Content = JsonContent.Create(request);

        try
        {
            HttpResponseMessage message  = await httpClient.SendAsync(httpRequest, token);
            string              rawBody  = await message.Content.ReadAsStringAsync(token);
            TResponse?          response = System.Text.Json.JsonSerializer.Deserialize<TResponse>(rawBody, _jsonOptions);

            if (response is PeopleResponse)
                logger.LogWarning("Elvanto {Uri} returned null after deserialization. Raw body: {Body}",
                    TRequest.RequestUri, rawBody);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Elvanto {Uri}: request failed", TRequest.RequestUri);
            return default;
        }
    }
}