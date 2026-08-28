using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;

/// <summary>
/// Opens named methods on a gRPC service to wall displays.
/// <para>
/// <b>Why this is not a <c>[Authorize]</c> attribute on the method.</b> protobuf-net.Grpc
/// only carries a method's attributes into endpoint metadata for methods declared
/// <i>directly</i> on the service's own contract. The generic reads every service inherits -
/// <c>BasicReadMultiple</c> and friends, declared on the <c>[SubService]</c> base interfaces -
/// lose theirs, and the endpoint ends up with no authorization metadata at all.
/// </para>
/// <para>
/// That failure is <b>silent and it fails closed</b>: the endpoint falls to the
/// <see cref="Policies.EnabledOnly"/> fallback, so the method keeps working for leaders and
/// turns displays away with a 401 that looks like a broken enrolment. It was found by a wall
/// sitting on "Connecting..." with a cookie that was perfectly good. Do not put the attribute
/// back on a <c>BasicReadMultiple</c> and assume it took effect.
/// </para>
/// <para>
/// So the allow-list lives at the mapping site in <c>Program.cs</c>, where the metadata is
/// added directly and cannot be dropped by anything in between. It stays an <b>allow</b>-list:
/// a method nobody names here is leader-only, so forgetting one still fails closed.
/// </para>
/// </summary>
public static class DisplayEndpointExtensions
{
    /// <summary>
    /// Marks the named methods on this service <see cref="Policies.EnabledOrDisplay"/>.
    /// Everything else on it stays leader-only by falling back.
    /// </summary>
    public static T AllowDisplay<T>(this T builder, params string[] methodNames)
        where T : IEndpointConventionBuilder
    {
        builder.Add(endpoint =>
        {
            // The route for a code-first gRPC method is "/{ServiceName}/{MethodName}", so the
            // last segment is the method. Matched on the route rather than the display name
            // because the display name is a human readable string with no contract.
            string? route = (endpoint as RouteEndpointBuilder)?.RoutePattern.RawText;

            if (route == null)
                return;

            string method = route[(route.LastIndexOf('/') + 1)..];

            if (!methodNames.Contains(method, StringComparer.Ordinal))
                return;

            endpoint.Metadata.Add(new AuthorizeAttribute { Policy = Policies.EnabledOrDisplay });
        });

        return builder;
    }
}
