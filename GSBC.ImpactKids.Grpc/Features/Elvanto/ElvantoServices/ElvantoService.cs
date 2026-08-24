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

    private async Task<TResponse?> SendMessage<TRequest, TResponse>(TRequest request, CancellationToken token = default)
        where TRequest : IRequestMessage
    {
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