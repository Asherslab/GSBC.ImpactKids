using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GSBC.ImpactKids.WASM.Authentication;

public sealed class BffAuthenticationStateProvider(
    HttpClient http
) : AuthenticationStateProvider
{
    private ClaimsPrincipal _cached = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var info = await http.GetFromJsonAsync<UserInfo>("/bff/user");
            if (info?.IsAuthenticated == true)
            {
                var identity = new ClaimsIdentity(
                    info.Claims.Select(c => new Claim(c.Type, c.Value)),
                    authenticationType: "cookie"
                );
                _cached = new ClaimsPrincipal(identity);
            }
            else
            {
                _cached = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        catch (Exception)
        {
            _cached = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new AuthenticationState(_cached);
    }

    private sealed record ClaimDto(
        string Type,
        string Value
    );

    private sealed record UserInfo(
        bool                  IsAuthenticated,
        IEnumerable<ClaimDto> Claims
    );
}