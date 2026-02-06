using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemoryVerseRequest : ReadRequestBase, IUpdateRequest<MemoryVerse, UpdateMemoryVerseRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> ReferenceName     { get; set; } = new();
    public DeltaUpdate<string> Verse             { get; set; } = new();
    public DeltaUpdate<Guid>   MemoryVerseListId { get; set; } = new();

    public static UpdateMemoryVerseRequest FromEntity(MemoryVerse entity)
    {
        UpdateMemoryVerseRequest request = new()
        {
            Guid = entity.Id
        };

        request.ReferenceName.SetInitialValue(entity.ReferenceName);
        request.Verse.SetInitialValue(entity.Verse);
        request.MemoryVerseListId.SetInitialValue(entity.MemoryVerseListId);

        return request;
    }
}