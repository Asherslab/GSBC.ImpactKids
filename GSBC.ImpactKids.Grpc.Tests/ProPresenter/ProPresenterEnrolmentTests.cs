using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.Grpc.Tests.ProPresenter;

/// <summary>
/// The rules that keep the ProPresenter connect page from being a phishing kit. The server's
/// <c>Approve</c>, the connect page and the agent all call these, so a regression here opens all three.
/// <para>
/// The loopback rule is the one that matters: PKCE does nothing against somebody who starts the flow
/// themselves and sends a leader the link, and only "the code may only go to this computer" stops
/// that. Each refused case below is a way a careless check has been fooled elsewhere.
/// </para>
/// </summary>
public class ProPresenterEnrolmentTests
{
    [Theory]
    [InlineData("http://127.0.0.1:50468/callback")]
    [InlineData("http://127.0.0.1:1/")]
    [InlineData("http://[::1]:50468/callback")]
    public void Loopback_redirects_are_accepted(string redirect) =>
        Assert.True(ProPresenterEnrolment.IsLoopbackRedirect(redirect));

    [Theory]
    [InlineData("http://evil.example/callback")]
    [InlineData("http://localhost:50468/callback")]        // RFC 8252 §8.3: may resolve elsewhere
    [InlineData("https://127.0.0.1:50468/callback")]       // nothing on the Mac serves TLS
    [InlineData("http://127.0.0.1@evil.example/callback")] // userinfo makes the host evil.example
    [InlineData("http://127.0.0.1.evil.example/callback")]
    [InlineData("http://127.0.0.1:50468/callback#x")]
    [InlineData("http://10.0.0.5:50468/callback")]         // another machine on the church network
    [InlineData("javascript:alert(1)")]
    [InlineData("/ProPresenter/Machines")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? redirect) =>
        Assert.False(ProPresenterEnrolment.IsLoopbackRedirect(redirect));

    /// <summary>The challenge must be S256 of the verifier, as computed independently by openssl.</summary>
    [Fact]
    public void Challenge_matches_an_independent_sha256()
    {
        // printf '%s' <verifier> | openssl dgst -sha256 -binary | openssl base64 | tr '+/' '-_' | tr -d '='
        const string verifier  = "dBjftJeZ4CVP-mJ92K9qbr1tKIfE48c1QA7Kv2VhmUA";
        const string challenge = "CYoY95REzJJmtBTnFjP42bormnIVocm2KAg-V8eVVYs";

        Assert.Equal(challenge, ProPresenterEnrolment.ComputeChallenge(verifier));
        Assert.True(ProPresenterEnrolment.VerifierMatches(verifier, challenge));
        Assert.False(ProPresenterEnrolment.VerifierMatches(verifier + "x", challenge));
    }

    [Theory]
    [InlineData("CYoY95REzJJmtBTnFjP42bormnIVocm2KAg-V8eVVYs", "S256", true)]
    [InlineData("CYoY95REzJJmtBTnFjP42bormnIVocm2KAg-V8eVVYs", "plain", false)] // plain protects nothing
    [InlineData("CYoY95REzJJmtBTnFjP42bormnIVocm2KAg-V8eVVY", "S256", false)]   // one short
    [InlineData("CYoY95REzJJmtBTnFjP42bormnIVocm2KAg+V8eVVYs", "S256", false)]  // not base64url
    [InlineData(null, "S256", false)]
    public void Only_a_well_formed_S256_challenge_is_accepted(string? challenge, string method, bool expected) =>
        Assert.Equal(expected, ProPresenterEnrolment.IsWellFormedChallenge(challenge, method));
}
