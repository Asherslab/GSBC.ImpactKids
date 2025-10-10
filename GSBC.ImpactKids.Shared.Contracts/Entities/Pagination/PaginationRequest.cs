namespace GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PaginationRequest
{
    public int Page { get; set; }
    public int PerPage { get; set; } = 10;
    public bool Disabled { get; set; }

    public PaginationRequest(int page = 0, int perPage = 10)
    {
        Page = page;
        PerPage = perPage;
    }

    protected PaginationRequest()
    {
    }

    public static PaginationRequest All()
    {
        return new PaginationRequest
        {
            Disabled = true
        };
    }
}