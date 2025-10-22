using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePersonRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string>  FirstName     { get; set; } = new();
    public DeltaUpdate<string>  LastName      { get; set; } = new();
    public DeltaUpdate<string?> PreferredName { get; set; } = new();
}