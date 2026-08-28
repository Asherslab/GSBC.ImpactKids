using Microsoft.Extensions.Caching.Memory;

namespace GSBC.ImpactKids.YARP.DisplayAuth;

/// <summary>
/// The proxy's half of the pickup display key. The key itself lives in the database behind
/// the gRPC service, so both questions about it are asked there:
/// <list type="bullet">
/// <item>at enrolment, "is this key the current one" - asked once, when the setup link is
/// opened. The answer also carries the token the screen presents to the gRPC service from
/// then on; this proxy never sees what signed it and holds no signing key of its own;</item>
/// <item>on every later request, "is the generation on this cookie still the current one" -
/// answered from a short lived cache, because a wall display reconnects its stream all
/// night.</item>
/// </list>
/// Both endpoints live under <c>internal/</c> on the gRPC service and have no proxy route,
/// so they are reachable from here and from nowhere outside the cluster.
/// </summary>
internal sealed class PickupDisplayKeyClient(
    HttpClient                        http,
    IMemoryCache                      cache,
    ILogger<PickupDisplayKeyClient>   logger
)
{
    private const string GenerationCacheKey = "pickup-display-key-generation";

    /// <summary>
    /// The last answer that actually arrived, kept so a gRPC service that is briefly
    /// unreachable does not sign every wall in the building out. A rotation that lands
    /// during such a blip simply takes effect when the service answers again.
    /// </summary>
    private static Guid? _lastKnownGeneration;

    /// <summary>
    /// Null when the key is wrong, or when no key has been minted yet. Otherwise the
    /// generation to stamp on the cookie and the bearer token to carry on it.
    /// </summary>
    public async Task<DisplayEnrolment?> ValidateAsync(string key, CancellationToken token)
    {
        HttpResponseMessage response = await http.PostAsJsonAsync(
            "internal/pickup-display-key/validate",
            new { Key = key },
            token
        );

        if (!response.IsSuccessStatusCode)
        {
            // Nothing about the attempt is logged, here or on the far side. The path is
            // enough to find this in a trace; the key is not going anywhere near a log.
            return null;
        }

        KeyGeneration? body = await response.Content.ReadFromJsonAsync<KeyGeneration>(token);

        // Both halves or nothing. A generation with no token would enrol a screen the proxy
        // is happy with and the gRPC service rejects, which shows up as a wall stuck on
        // "Connecting..." with a valid looking cookie.
        return body?.Generation is { } generation && !string.IsNullOrEmpty(body.Token)
            ? new DisplayEnrolment(generation, body.Token)
            : null;
    }

    /// <summary>
    /// Which key is current, cached for
    /// <see cref="DisplayAuthOptions.GenerationCacheLifetime"/> so a wall reconnecting its
    /// stream all night is not a query per reconnect.
    /// </summary>
    public async Task<Guid?> CurrentGenerationAsync(CancellationToken token)
    {
        if (cache.TryGetValue(GenerationCacheKey, out Guid? cached))
            return cached;

        try
        {
            KeyGeneration? body = await http.GetFromJsonAsync<KeyGeneration>(
                "internal/pickup-display-key/generation",
                token
            );

            _lastKnownGeneration = body?.Generation;

            cache.Set(GenerationCacheKey, _lastKnownGeneration, DisplayAuthOptions.GenerationCacheLifetime);

            return _lastKnownGeneration;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not read the current pickup display key generation");

            // Not cached - the next request tries again. Falling back to the last answer
            // that arrived keeps the walls up through a blip; only a proxy that has never
            // managed the read at all turns screens away.
            return _lastKnownGeneration;
        }
    }

    private sealed record KeyGeneration(Guid? Generation, string? Token);

    /// <summary>What a screen walks away from enrolment with.</summary>
    internal sealed record DisplayEnrolment(Guid Generation, string Token);
}
