using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTermsRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public int? Year { get; set; }
}