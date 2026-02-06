using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemoryVerseListRequest
    : ReadRequestBase, IUpdateRequest<MemoryVerseList, UpdateMemoryVerseListRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> Name         { get; set; } = new();
    public DeltaUpdate<Guid?>  SchoolTermId { get; set; } = new();

    public static UpdateMemoryVerseListRequest FromEntity(MemoryVerseList entity)
    {
        UpdateMemoryVerseListRequest request = new()
        {
            Guid = entity.Id
        };

        request.Name.SetInitialValue(entity.Name);
        request.SchoolTermId.SetInitialValue(entity.SchoolTermId);

        return request;
    }
}