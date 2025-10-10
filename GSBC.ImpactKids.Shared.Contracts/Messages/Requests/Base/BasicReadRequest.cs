using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BasicReadRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;
}