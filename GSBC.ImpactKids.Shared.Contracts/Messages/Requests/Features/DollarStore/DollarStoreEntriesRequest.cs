namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DollarStore;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class DollarStoreEntriesRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public Guid? SchoolTermId { get; set; }
}