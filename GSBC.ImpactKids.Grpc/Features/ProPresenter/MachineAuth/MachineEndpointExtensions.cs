using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

public static class MachineEndpointExtensions
{
    /// <summary>
    /// Puts every method of this service behind <see cref="Policies.MachineOnly"/>, at the mapping
    /// site, for the reason <c>DisplayEndpointExtensions</c> gives: attributes on methods do not
    /// reliably reach endpoint metadata through protobuf-net.Grpc. Use it only on
    /// <c>ProPresenterAgentService</c>. A leader can call none of it, and a machine nothing else.
    /// </summary>
    public static T RequireMachine<T>(this T builder)
        where T : IEndpointConventionBuilder
    {
        builder.Add(endpoint => endpoint.Metadata.Add(new AuthorizeAttribute { Policy = Policies.MachineOnly }));

        return builder;
    }
}
