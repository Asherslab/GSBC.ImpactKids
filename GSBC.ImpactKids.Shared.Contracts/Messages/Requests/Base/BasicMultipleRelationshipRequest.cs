namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BasicMultipleRelationshipRequest<FirstEntity, SecondEntity>
{
    public required Guid FirstId  { get; set; }
    public required Guid SecondId { get; set; }
}