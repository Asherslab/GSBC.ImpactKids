namespace GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record PaginationRequest
{
    public int  Page     { get; init; } = 0;
    public int  PerPage  { get; init; } = 10;
    public bool Disabled { get; init; }

    // exists because it's more compact than obj initializers is
    public PaginationRequest(int page = 0, int perPage = 10)
    {
        Page = page;
        PerPage = perPage;
    }

    // for GRPC construction
    public PaginationRequest()
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