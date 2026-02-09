using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Authentication;

[Service("gRPC/GSBC.ImpactKids.Users")]
public interface IUsersService : IBasicReadMultipleService<User>
{
    Task<BasicResponse?> ToggleEnabled(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}