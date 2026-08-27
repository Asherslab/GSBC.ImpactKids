namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

/// <summary>
/// What both sides held the last time they agreed about one field on one person — the base value
/// of a three-way merge, not a memo of what Elvanto last said.
///
/// <b>A null <see cref="AppHash"/> means there is no base</b>, and the field falls back to first-sync
/// rules. The column was added without a backfill deliberately: the first run after it lands
/// re-applies those rules to everything and surfaces every divergence that was invisible before,
/// including every app value Elvanto has never been told about.
///
/// The <c>LastSeen*</c> names are the Elvanto leg and are historical — renaming them, or the table,
/// is a destructive migration and needs asking first.
/// </summary>
public class DbElvantoFieldSnapshot
{
    public required Guid           Id           { get; set; }
    public required string         EntityType   { get; set; }
    public required Guid           EntityId     { get; set; }
    public required string         FieldName    { get; set; }

    /// <summary>The Elvanto leg of the base: what Elvanto held at the last agreement.</summary>
    public required string         LastSeenHash { get; set; }

    /// <inheritdoc cref="LastSeenHash"/>
    public          string?        LastSeenValue { get; set; }

    /// <summary>
    /// The app leg of the base: what the app held at the last agreement. Null means no base — this
    /// row predates the column, or the field has never settled.
    /// </summary>
    public          string?        AppHash      { get; set; }

    /// <inheritdoc cref="AppHash"/>
    public          string?        AppValue     { get; set; }

    /// <summary>
    /// When the two sides last agreed. Not "when this app last polled Elvanto" — the two were
    /// conflated, and comparing a poll timestamp against an edit timestamp is what buried real
    /// changes. Nothing decides on this any more; it is kept for the audit trail.
    /// </summary>
    public required DateTimeOffset LastSeenAt   { get; set; }
}
