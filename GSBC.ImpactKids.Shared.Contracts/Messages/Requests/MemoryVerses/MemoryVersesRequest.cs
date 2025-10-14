using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVersesRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public Guid? MemoryVerseListId { get; set; }
    public Guid? ServiceId { get; set; }
}