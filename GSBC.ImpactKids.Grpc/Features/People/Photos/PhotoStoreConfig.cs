namespace GSBC.ImpactKids.Grpc.Features.People.Photos;

/// <summary>
/// Where the photo objects live. Bound from the <c>Photos</c> configuration section.
///
/// The store is S3-compatible but is not AWS: locally it is the SeaweedFS container Aspire runs, in
/// production it is the one in the cluster. Both are reached by explicit endpoint with path-style
/// addressing, because virtual-host addressing needs DNS per bucket and neither has it.
/// </summary>
public class PhotoStoreConfig
{
    public const string SectionName = "Photos";

    /// <summary>e.g. <c>http://localhost:60537</c>. Cluster-internal in production — no ingress.</summary>
    public required string ServiceUrl { get; set; }

    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }

    public string BucketName { get; set; } = "photos";

    /// <summary>
    /// Media consent values that may not hold a photo, as a comma-separated list of
    /// <c>MediaConsent</c> names. <b>Empty by default, deliberately.</b>
    ///
    /// An identification photo for signing a child in is internal safeguarding, not publication, so
    /// the default is that everyone gets one. Setting this to <c>StrictlyNo</c> later suppresses
    /// capture, hides the control and stops the backfill for those people with no code change,
    /// because it is a policy question for the church rather than an engineering one.
    /// </summary>
    public string BlockedMediaConsent { get; set; } = "";

    /// <summary>
    /// Whether this person's media consent forbids them a photo.
    ///
    /// Lives here rather than on either caller because both the upload endpoint and the backfill
    /// worker have to agree: a policy that stops a leader taking a photo but lets the backfill pull
    /// one in anyway is not a policy.
    /// </summary>
    public bool IsBlockedByMediaConsent(string? mediaConsent) =>
        !string.IsNullOrWhiteSpace(BlockedMediaConsent)
        && BlockedMediaConsent
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(blocked => string.Equals(blocked, mediaConsent, StringComparison.OrdinalIgnoreCase));
}
