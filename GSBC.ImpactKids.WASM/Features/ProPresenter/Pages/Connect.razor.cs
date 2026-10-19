using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using static GSBC.ImpactKids.Shared.Contracts.ErrorConstants;

namespace GSBC.ImpactKids.WASM.Features.ProPresenter.Pages;

/// <summary>
/// The approval step of connecting a ProPresenter machine - the "authorize" page of RFC 8252. The
/// agent on the Mac opens this with its loopback address and PKCE challenge on the query string; a
/// leader approves; the browser carries the single-use code back to the agent.
/// <para>
/// Sign in is required like every page under Features, and the login redirect keeps the whole URL,
/// so a leader who was signed out lands back here with the query intact.
/// </para>
/// <para>
/// The checks here are for a readable error before anybody presses anything. They are not the
/// control: the server repeats them in <c>Approve</c>, because this page runs in a browser that can
/// be made to do anything.
/// </para>
/// </summary>
public partial class Connect
{
    [SupplyParameterFromQuery(Name = "redirect_uri")]
    public string? RedirectUri { get; set; }

    [SupplyParameterFromQuery(Name = "state")]
    public string? State { get; set; }

    [SupplyParameterFromQuery(Name = "code_challenge")]
    public string? CodeChallenge { get; set; }

    [SupplyParameterFromQuery(Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    /// <summary>The Mac's own name, offered by the agent as a starting point. Only ever pre-fills a text box.</summary>
    [SupplyParameterFromQuery(Name = "name")]
    public string? SuggestedName { get; set; }

    private string? _name;
    private string? _linkError;
    private bool    _busy;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (!ProPresenterEnrolment.IsLoopbackRedirect(RedirectUri))
            _linkError = ProPresenterRedirectInvalid;
        else if (string.IsNullOrEmpty(State) || !ProPresenterEnrolment.IsWellFormedChallenge(CodeChallenge, CodeChallengeMethod))
            _linkError = ProPresenterChallengeInvalid;
        else
            _linkError = null;

        _name ??= SuggestedName?.Trim() is { Length: > 0 } suggested
            ? suggested[..Math.Min(suggested.Length, 100)]
            : null;
    }

    private async Task Approve()
    {
        _busy = true;

        try
        {
            BasicReadResponse<string> response = await MachineService.Approve(new ApproveProPresenterMachineRequest
            {
                Name = _name ?? "",
                RedirectUri = RedirectUri!,
                CodeChallenge = CodeChallenge!,
                CodeChallengeMethod = CodeChallengeMethod!
            });

            if (response.HasErrorOrNull() || response.Entity == null)
            {
                Snackbar.AddErrorResponse(response);
                return;
            }

            // A top level navigation to the Mac's own loopback address, not a fetch: the agent is
            // listening there and answers with a page saying it is connected.
            ReturnToAgent(("code", response.Entity), ("state", State!));
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>RFC 6749 §4.1.2.1 - tell the agent no, so it stops waiting and can say so.</summary>
    private void Deny() => ReturnToAgent(("error", "access_denied"), ("state", State!));

    private void ReturnToAgent(params (string Key, string Value)[] parameters)
    {
        string query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        string separator = RedirectUri!.Contains('?') ? "&" : "?";

        Navigation.NavigateTo($"{RedirectUri}{separator}{query}", forceLoad: true);
    }
}
