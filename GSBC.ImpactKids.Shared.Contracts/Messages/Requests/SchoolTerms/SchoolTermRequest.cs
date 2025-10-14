using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTermRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public bool ThisTerm { get; set; }
}