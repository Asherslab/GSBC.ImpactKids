using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemoryVerseListRequest : ReadRequestBase
{
    public override string              Id   { get; set; } = null!;
    public          DeltaUpdate<string> Name { get; set; } = new();

    public DeltaUpdate<Guid?> SchoolTermId { get; set; } = new();
}