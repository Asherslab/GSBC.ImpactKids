namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTermRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public bool ThisTerm { get; set; }
}