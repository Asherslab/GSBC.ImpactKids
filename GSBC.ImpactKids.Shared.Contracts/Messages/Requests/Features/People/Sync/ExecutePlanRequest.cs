namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ExecutePlanRequest
{
    /// <summary>The operation whose plan should be executed.</summary>
    public Guid OperationId { get; set; }
}
