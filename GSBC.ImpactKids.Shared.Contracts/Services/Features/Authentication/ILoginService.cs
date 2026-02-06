namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Authentication;

[Service("GSBC.ImpactKids.Login")]
public interface ILoginService
{
    Task<BasicReadResponse<bool>?> IsUserEnabled(
        BasicReadRequest request,
        CallContext    context = default
    );
}