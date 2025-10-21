using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServiceRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public bool PreviousService { get; set; }
    public bool UpcomingService { get; set; }
}