namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ManualReviewActionRequest
{
    public Guid Id { get; set; }
}
