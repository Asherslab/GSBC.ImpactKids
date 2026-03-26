namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface ICreateService<in TCreateRequest>
{
    Task<BasicReadResponse<Guid?>> Create(
        TCreateRequest request,
        CallContext    context = default
    );
};