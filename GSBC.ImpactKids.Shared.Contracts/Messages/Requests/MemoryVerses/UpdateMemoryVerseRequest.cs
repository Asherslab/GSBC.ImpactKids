using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemoryVerseRequest : ReadRequestBase
{
    public override string              Id   { get; set; } = null!;
    public          DeltaUpdate<string> Reference { get; set; } = new();

    public DeltaUpdate<Guid> SchoolTermId { get; set; } = new();
}