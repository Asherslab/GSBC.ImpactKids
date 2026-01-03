namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IUpdateService<in TUpdateRequest>
{
    Task<BasicResponse> Update(
        TUpdateRequest request,
        CallContext    context = default
    );
};