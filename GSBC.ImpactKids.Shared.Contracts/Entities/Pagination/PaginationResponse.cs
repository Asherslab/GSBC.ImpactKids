// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PaginationResponse
{
    public int Page { get; set; }
    public int PerPage { get; set; } = 10;
    public int Total { get; set; }
    public bool Disabled { get; set; }

    public PaginationResponse(int total, int page = 0, int perPage = 10)
    {
        Page = page;
        PerPage = perPage;
        Total = total;
    }

    public PaginationResponse(PaginationRequest request, int total) : this(total, request.Page, request.PerPage)
    {
        Disabled = request.Disabled;
    }

    protected PaginationResponse()
    {
    }

    public static PaginationResponse Empty()
    {
        return new PaginationResponse(0, 0, 0);
    }
}