using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IReadMultipleServiceBase<TEntity, in TRequest, TResponse>
    where TRequest : IReadMultipleRequest
    where TResponse : IReadMultipleResponse<TEntity>
{
    Task<TResponse> ReadMultiple(TRequest request, CallContext context = default);
}

[SubService]
public interface IReadMultipleServiceBase
    <TEntity> : IReadMultipleServiceBase<TEntity, BasicReadMultipleRequest, BasicReadMultipleResponse<TEntity>>;