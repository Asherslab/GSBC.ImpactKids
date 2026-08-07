using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Authentication;

public sealed class BffAuthenticationStateProvider(
    HttpClient http,
    IJSRuntime js
) : AuthenticationStateProvider
{
    private const string CacheKey = "auth:lastKnownUser";

    /// <summary>
    /// How long an offline device keeps trusting its last successful sign in.
    /// This only gates client side UI - every gRPC call is still authorised on the
    /// server - but it is what lets the point tracker open with no reception.
    /// </summary>
    private static readonly TimeSpan CacheValidFor = TimeSpan.FromDays(14);

    private ClaimsPrincipal _cached = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            UserInfo? info = await http.GetFromJsonAsync<UserInfo>("/bff/user");

            if (info?.IsAuthenticated == true)
            {
                _cached = BuildPrincipal(info.Claims);
                await WriteCacheAsync(info.Claims);
            }
            else
            {
                // The server answered and said no. Not an offline case - drop the cache.
                _cached = new ClaimsPrincipal(new ClaimsIdentity());
                await ClearCacheAsync();
            }
        }
        catch (Exception)
        {
            // Could not reach the server. Fall back to the last known sign in so the
            // app still opens offline, rather than bouncing the user to a login page
            // they cannot load.
            _cached = await ReadCacheAsync() ?? new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new AuthenticationState(_cached);
    }

    private static ClaimsPrincipal BuildPrincipal(IEnumerable<ClaimDto> claims) =>
        new(new ClaimsIdentity(
                claims.Select(c => new Claim(c.Type, c.Value)),
                authenticationType: "cookie"
            )
        );

    private async Task<ClaimsPrincipal?> ReadCacheAsync()
    {
        try
        {
            string? raw = await js.InvokeAsync<string?>("localStorage.getItem", CacheKey);

            if (string.IsNullOrWhiteSpace(raw))
                return null;

            CachedUser? cached = JsonSerializer.Deserialize(raw, AuthJsonContext.Default.CachedUser);

            if (cached == null || DateTimeOffset.UtcNow - cached.CachedAt > CacheValidFor)
                return null;

            return BuildPrincipal(cached.Claims);
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteCacheAsync(IEnumerable<ClaimDto> claims)
    {
        try
        {
            CachedUser cached = new(DateTimeOffset.UtcNow, claims.ToList());
            string     json   = JsonSerializer.Serialize(cached, AuthJsonContext.Default.CachedUser);

            await js.InvokeVoidAsync("localStorage.setItem", CacheKey, json);
        }
        catch
        {
            // ignored - offline resume is a nicety, not a requirement for staying signed in
        }
    }

    private async Task ClearCacheAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", CacheKey);
        }
        catch
        {
            // ignored
        }
    }

    internal sealed record ClaimDto(
        string Type,
        string Value
    );

    private sealed record UserInfo(
        bool                  IsAuthenticated,
        IEnumerable<ClaimDto> Claims
    );

    internal sealed record CachedUser(
        DateTimeOffset CachedAt,
        List<ClaimDto> Claims
    );
}

[JsonSerializable(typeof(BffAuthenticationStateProvider.CachedUser))]
internal partial class AuthJsonContext : JsonSerializerContext;
