namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IBasicReadMultipleService
    <TEntity>
{
    Task<BasicReadMultipleResponse<TEntity>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
};