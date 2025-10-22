using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Login;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Login")]
public interface ILoginService
{
    Task<BasicReadResponse<bool>?> IsUserEnabled(
        BasicReadRequest request,
        CallContext    context = default
    );

    Task<BasicResponse?> CreateSelf(
        CreateSelfRequest request,
        CallContext context = default
    );
}