namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServiceRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public bool PreviousService { get; set; }
    public bool UpcomingService { get; set; }
}