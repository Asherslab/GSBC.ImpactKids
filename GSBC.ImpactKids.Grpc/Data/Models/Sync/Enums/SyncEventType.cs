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
    Archived
}
