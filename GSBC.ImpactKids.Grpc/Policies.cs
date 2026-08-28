using GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc;

/// <summary>
/// Who may call what. There are two kinds of caller and they are kept apart here rather
/// than by convention:
/// <list type="bullet">
/// <item><b>a leader</b> - a signed in person with an enabled user row;</item>
/// <item><b>a display</b> - a screen on a wall, enrolled on the pickup display key.</item>
/// </list>
/// <para>
/// <b>Every policy names its schemes.</b> That is what makes the separation structural: a
/// display token does not merely fail <see cref="EnabledOnly"/>, it is never authenticated
/// against it in the first place, so no claim it could ever carry can satisfy that policy.
/// </para>
/// <para>
/// <b>There is no class level authorization anywhere in this service, on purpose.</b> The
/// fallback policy is <see cref="EnabledOnly"/>, so an endpoint with no attribute at all is
/// leader only - a method somebody forgets to annotate fails closed. The annotation burden
/// is therefore inverted: you never mark a write, you mark only the <i>reads a display is
/// allowed to make</i>, with <see cref="EnabledOrDisplay"/>. Class level attributes would
/// undo this - a broad attribute on the class plus a narrow one missing from a method is
/// exactly the fail-open case this arrangement exists to prevent.
/// </para>
/// </summary>
public static class Policies
{
    /// <summary>A signed in, enabled leader. The fallback, and the default for everything.</summary>
    public const string EnabledOnly = "EnabledOnly";

    /// <summary>An enrolled wall display and nothing else. No leader satisfies this.</summary>
    public const string DisplayOnly = "DisplayOnly";

    /// <summary>
    /// Either caller. The <b>only</b> policy that lets a display in, so every display
    /// reachable endpoint in the service is found by searching for this name.
    /// <para>
    /// Read methods only. Nothing enforces that in the policy itself - it cannot know a read
    /// from a write - so <see cref="Data.Interceptors.DisplayReadOnlyInterceptor"/> enforces
    /// it at the database instead, and this attribute on a write would be refused there.
    /// </para>
    /// </summary>
    public const string EnabledOrDisplay = "EnabledOrDisplay";

    public static AuthorizationOptions AddGsbcPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(EnabledOnly, policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim("Enabled", true.ToString())
        );

        options.AddPolicy(DisplayOnly, policy => policy
            .AddAuthenticationSchemes(DisplayAuthDefaults.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(DisplayAuthDefaults.GenerationClaimType)
        );

        options.AddPolicy(EnabledOrDisplay, policy => policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                DisplayAuthDefaults.SchemeName
            )
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim("Enabled", true.ToString())
                || context.User.HasClaim(claim => claim.Type == DisplayAuthDefaults.GenerationClaimType)
            )
        );

        // Anything not annotated at all. Leader only, so a new method is protected before
        // anybody remembers to think about it - see the class remarks.
        options.FallbackPolicy = options.GetPolicy(EnabledOnly);

        return options;
    }
}
