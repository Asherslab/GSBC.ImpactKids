using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record BasicReadMultipleResponse<T> : IReadMultipleResponse<T>
{
    public ImmutableList<T>   Entities   { get; init; } = ImmutableList<T>.Empty;
    public PaginationResponse Pagination { get; init; } = PaginationResponse.Empty();

    public required bool    Success { get; init; }
    public          string? Error   { get; init; }
}