using System.Net.Http.Headers;
using System.Text;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Interfaces;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Services;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

public partial class ElvantoService(
    HttpClient httpClient,
    ElvantoConfig config
) : IElvantoService
{
    private async Task<TResponse?> SendMessage<TRequest, TResponse>(TRequest request)
        where TRequest : IRequestMessage
    {
        HttpRequestMessage httpRequest = new(HttpMethod.Post, TRequest.RequestUri);
        string             encoded     = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.Authentication));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage message = await httpClient.SendAsync(httpRequest);
        return await message.Content.ReadFromJsonAsync<TResponse>();
    }
}