namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IBasicReadMultipleService<TEntity>
{
    IAsyncEnumerable<BasicReadMultipleResponse<TEntity>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
};