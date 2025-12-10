using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemoryVerseRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> ReferenceName     { get; set; } = new();
    public DeltaUpdate<string> Verse             { get; set; } = new();
    public DeltaUpdate<Guid>   MemoryVerseListId { get; set; } = new();

    public DeltaUpdate<Guid[]> ServiceIds    { get; set; } = new();
    public DeltaUpdate<Guid[]> BibleVerseIds { get; set; } = new();
}