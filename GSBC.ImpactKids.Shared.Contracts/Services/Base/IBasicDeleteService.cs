namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IBasicDeleteService<TEntity>
{
    Task<BasicResponse> BasicDelete(
        BasicReadRequest request,
        CallContext      context = default
    );
};