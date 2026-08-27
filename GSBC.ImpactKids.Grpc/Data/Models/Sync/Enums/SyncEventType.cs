namespace GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

public enum SyncEventType
{
    Match,
    FieldUpdated,
    Conflict,
    Created,
    PushedToElvanto,
    WouldPushToElvanto,
    WouldCreateInElvanto,
    ManualReviewQueued,
    Archived,

    /// <summary>
    /// The two sides hold different values and the engine deliberately did nothing about it.
    ///
    /// Nine of the nineteen paths through the field loop end in no action, no audit row and no
    /// counter, which makes a real divergence indistinguishable from nothing to do - and that is
    /// the reported symptom, not a side effect of it. Every one of those paths now writes one of
    /// these, carrying both values, so silence stops being a valid outcome.
    /// </summary>
    Diverged
}
