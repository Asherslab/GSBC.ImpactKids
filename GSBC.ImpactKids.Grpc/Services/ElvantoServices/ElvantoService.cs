using System.Net.Http.Headers;
using System.Text;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Interfaces;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class ElvantoService(
    HttpClient httpClient,
    ElvantoConfig config
) : IElvantoService
{
    private async Task<TResponse?> SendMessage<TRequest, TResponse>(TRequest request, CancellationToken token = default)
        where TRequest : IRequestMessage
    {
        HttpRequestMessage httpRequest = new(HttpMethod.Post, TRequest.RequestUri);
        string             encoded     = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.Authentication));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage message = await httpClient.SendAsync(httpRequest, token);
        return await message.Content.ReadFromJsonAsync<TResponse>(cancellationToken: token);
    }
}