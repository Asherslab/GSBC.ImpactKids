using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using MudBlazor;
using Metadata = Grpc.Core.Metadata;

namespace GSBC.ImpactKids.WASM.Authentication;

public class CustomAccountFactory(
    IAccessTokenProviderAccessor accessor,
    ILoginService                loginService,
    ISnackbar                    snackbar
)
    : AccountClaimsPrincipalFactory<RemoteUserAccount>(accessor)
{
    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount               account,
        RemoteAuthenticationUserOptions options
    )
    {
        ClaimsPrincipal initialUser = await base.CreateUserAsync(account, options);

        if (initialUser.Identity is not { IsAuthenticated: true })
            return initialUser;
        
        ClaimsIdentity userIdentity = (ClaimsIdentity) initialUser.Identity;

        AccessTokenResult result = await TokenProvider.RequestAccessToken();

        if (!result.TryGetToken(out AccessToken? accessToken))
            return initialUser;
        
        Metadata metadata = new() { { "Authorization", $"Bearer {accessToken.Value}" } };

        CallOptions callOptions = new(metadata);
        BasicReadResponse<bool>? resp = await loginService.IsUserEnabled(
            new BasicReadRequest(),
            callOptions
        );
        
        if (resp.HasErrorOrNull())
        {
            snackbar.AddErrorResponse(resp);
            return initialUser;
        }
                
        userIdentity.AddClaim(new Claim("Enabled", resp.Entity.ToString()));

        return initialUser;
    }
}