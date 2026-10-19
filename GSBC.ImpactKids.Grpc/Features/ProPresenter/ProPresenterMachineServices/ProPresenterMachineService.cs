using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.ProPresenter;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterMachineServices;

/// <summary>
/// The admin end of the ProPresenter machines - see <see cref="IProPresenterMachineService"/>.
/// Leader only by carrying no authorization attribute: the fallback policy closes it to everyone
/// else, and a machine token never authenticates against that policy's scheme at all.
/// </summary>
public partial class ProPresenterMachineService(
    GsbcDbContext                                              db,
    IConverter<DbProPresenterMachine, ProPresenterMachine>     converter,
    IEventService<ProPresenterMachine>                         eventService,
    MachineSigningKeyProvider                                  signingKeys
) : IProPresenterMachineService
{
    /// <summary>ProPresenter's API is usually on the same Mac as the agent.</summary>
    private const string DefaultProPresenterHost = "127.0.0.1";

    /// <summary>
    /// ProPresenter picks no fixed port - it is whatever was typed into its Network preferences - so
    /// this is only a starting value for the admin page to correct. 55980 is the Impact Kids Mac's.
    /// </summary>
    private const int DefaultProPresenterPort = 55980;
}
