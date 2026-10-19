using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterMachineServices;

public partial class ProPresenterMachineService
{
    /// <summary>
    /// A leader saying yes on the connect page. Creates the machine and returns the code the page
    /// hands to the agent.
    /// <para>
    /// <b>The loopback check is here, not only on the page</b>, because the page is the attacker's
    /// to edit. It is the one rule that stops this method minting codes for a link somebody else
    /// started - see <see cref="ProPresenterEnrolment.IsLoopbackRedirect"/>.
    /// </para>
    /// <para>
    /// Every approval creates a new machine; nothing re-enrols an old row. A Mac whose Keychain was
    /// wiped comes back as a new machine, and the old one is deleted from the admin page.
    /// </para>
    /// </summary>
    public async Task<BasicReadResponse<string>> Approve(
        ApproveProPresenterMachineRequest request,
        CallContext                       context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string? userId = context.ServerCallContext?.GetHttpContext().User.FindFirstValue("UserId");

        if (userId == null)
            return BasicReadResponse<string>.WithError(PermissionDenied);

        string name = request.Name.Trim();

        if (string.IsNullOrEmpty(name))
            return BasicReadResponse<string>.WithError(ProPresenterMachineNameNull);

        if (!ProPresenterEnrolment.IsLoopbackRedirect(request.RedirectUri))
            return BasicReadResponse<string>.WithError(ProPresenterRedirectInvalid);

        if (!ProPresenterEnrolment.IsWellFormedChallenge(request.CodeChallenge, request.CodeChallengeMethod))
            return BasicReadResponse<string>.WithError(ProPresenterChallengeInvalid);

        string code = MachineSecrets.Generate();

        db.ProPresenterMachines.Add(new DbProPresenterMachine
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.Parse(userId),
            TokenSigningKey = MachineSecrets.GenerateSigningKey(),
            EnrolmentCodeHash = MachineSecrets.Hash(code),
            EnrolmentChallenge = request.CodeChallenge,
            EnrolmentRedirectUri = request.RedirectUri,
            EnrolmentExpiresAt = DateTimeOffset.UtcNow.Add(MachineAuthDefaults.EnrolmentCodeLifetime),
            ProPresenterHost = DefaultProPresenterHost,
            ProPresenterPort = DefaultProPresenterPort
        });

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        // The code's one appearance in the clear. No log line, here or at the exchange.
        return new BasicReadResponse<string>
        {
            Success = true,
            Entity = code
        };
    }
}
