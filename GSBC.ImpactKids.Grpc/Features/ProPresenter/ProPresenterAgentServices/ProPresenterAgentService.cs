using Grpc.Core;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.ProPresenter;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

/// <summary>
/// What a ProPresenter machine may call - see <see cref="IProPresenterAgentService"/>. Mapped with
/// <see cref="MachineEndpointExtensions.RequireMachine{T}"/>, so no leader and no display reaches it.
/// </summary>
public partial class ProPresenterAgentService(
    GsbcDbContext                      db,
    IEventService<ProPresenterMachine> eventService,
    IEventService<Song>                songEventService,
    IConverter<DbSchedule, Schedule>   scheduleConverter
) : IProPresenterAgentService
{
    /// <summary>
    /// The caller, from its token. Never null on a correctly mapped method - the policy has already
    /// required the claim - so a null here is a mapping mistake and is refused rather than guessed.
    /// </summary>
    private static Guid? CallingMachine(CallContext context) =>
        context.ServerCallContext == null
            ? null
            : MachineTokens.MachineId(context.ServerCallContext.GetHttpContext().User);
}
