using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Users")]
public interface IUsersService
{
    Task<BasicReadMultipleResponse<User>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );

    Task<BasicResponse?> ToggleEnabled(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}