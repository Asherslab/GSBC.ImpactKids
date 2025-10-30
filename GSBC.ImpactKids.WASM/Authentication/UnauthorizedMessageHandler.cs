using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace GSBC.ImpactKids.WASM.Authentication;

public class UnauthorizedMessageHandler(
    NavigationManager navigationManager
) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken
    )
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized) return response;

        string currentUrl = navigationManager.ToBaseRelativePath(navigationManager.Uri);
        // string loginUrl   = $"authentication/Login?ReturnUrl={currentUrl}";
        
        InteractiveRequestOptions requestOptions = new()
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = currentUrl
        };
        
        navigationManager.NavigateToLogin("authentication/Login", requestOptions);

        return response;
    }
}