using System.Net;
using System.Net.Http.Json;
using GSBC.ImpactKids.ProPresenterAgent.Credentials;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.ProPresenterAgent.ImpactKids;

/// <summary>Impact Kids has said this machine's credential is dead - revoked, or never existed. Set up again.</summary>
public sealed class CredentialRevokedException() : Exception("Impact Kids no longer recognises this machine");

/// <summary>
/// The agent's side of <see cref="ProPresenterEnrolment.TokenPath"/>: spends an approval code once,
/// then trades the machine secret for fifteen-minute access tokens.
/// <para>
/// <b>Only an explicit <c>invalid_client</c> means the credential is dead.</b> A network failure, a
/// 5xx, Cloudflare having a bad minute - all of those are retried, never treated as revocation. A Mac
/// that wiped its credential every time the church internet blinked would open a browser on the
/// projector mid-service.
/// </para>
/// </summary>
public sealed class TokenClient(HttpClient http)
{
    /// <summary>Refreshed this long before it expires, so a call never sets off with a token about to lapse.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private string?  _accessToken;
    private DateTime _expiresAt;

    public async Task<MachineCredential> ExchangeCodeAsync(
        string            code,
        string            verifier,
        string            redirectUri,
        CancellationToken token
    )
    {
        ProPresenterTokenResponse response = await PostAsync(new ProPresenterTokenRequest
        {
            GrantType = ProPresenterTokenRequest.AuthorizationCodeGrant,
            Code = code,
            CodeVerifier = verifier,
            RedirectUri = redirectUri
        }, token);

        if (response.MachineId == null || string.IsNullOrEmpty(response.MachineSecret))
            throw new InvalidOperationException($"Impact Kids refused the connection ({response.Error ?? "no credential returned"})");

        return new MachineCredential(response.MachineId.Value, response.MachineSecret);
    }

    public async Task<string> GetAccessTokenAsync(MachineCredential credential, CancellationToken token)
    {
        await _gate.WaitAsync(token);

        try
        {
            if (_accessToken != null && DateTime.UtcNow < _expiresAt - RefreshMargin)
                return _accessToken;

            ProPresenterTokenResponse response = await PostAsync(new ProPresenterTokenRequest
            {
                GrantType = ProPresenterTokenRequest.ClientCredentialsGrant,
                MachineId = credential.MachineId,
                MachineSecret = credential.Secret
            }, token);

            if (response.AccessToken == null || response.ExpiresIn == null)
                throw new InvalidOperationException($"Impact Kids gave no access token ({response.Error ?? "empty response"})");

            _accessToken = response.AccessToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn.Value);

            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>After a call came back unauthenticated - the next one fetches a fresh token rather than retrying a dead one.</summary>
    public void Forget() => _accessToken = null;

    private async Task<ProPresenterTokenResponse> PostAsync(ProPresenterTokenRequest request, CancellationToken token)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(
            ProPresenterEnrolment.TokenPath.TrimStart('/'), request, token);

        ProPresenterTokenResponse? body = null;

        // A Cloudflare error page, or anything else that is not ours, is simply not JSON.
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
            body = await response.Content.ReadFromJsonAsync<ProPresenterTokenResponse>(token);

        if (response.StatusCode == HttpStatusCode.Unauthorized
            && body?.Error == ProPresenterTokenResponse.InvalidClient)
            throw new CredentialRevokedException();

        if (body == null)
            throw new HttpRequestException(
                $"Impact Kids token endpoint answered {(int)response.StatusCode} with no usable body",
                null, response.StatusCode);

        return body;
    }
}
