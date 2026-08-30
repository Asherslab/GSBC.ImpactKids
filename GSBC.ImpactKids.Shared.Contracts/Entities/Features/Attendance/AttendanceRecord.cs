namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record AttendanceRecord : IIdentifiable
{
    public required Guid Id { get; init; }

    public required DateTime  SignedIn  { get; init; }
    public required DateTime? SignedOut { get; init; }

    [ProtoIgnore]
    public DateTime LocalSignedIn => SignedIn.ToLocalTime();

    [ProtoIgnore]
    public DateTime? LocalSignedOut => SignedOut?.ToLocalTime();

    /// <summary>When a parent asked for this child. Null means never requested.</summary>
    public DateTime? PickupRequested { get; init; }

    public Guid? PickupRequestedUserId { get; init; }

    [ProtoIgnore]
    public DateTime? LocalPickupRequested => PickupRequested?.ToLocalTime();

    /// <summary>On the wall: asked for, and not yet gone.</summary>
    [ProtoIgnore]
    public bool AwaitingPickup => PickupRequested != null && SignedOut == null;

    public bool Deleted { get; init; }

    public required Guid  PersonId        { get; init; }
    public required Guid  SignedInUserId  { get; init; }
    public          Guid? SignedOutUserId { get; init; }
    public required Guid  ServiceId       { get; init; }

    /// <summary>
    /// Whether this person is currently in the building — signed in for the night and not signed
    /// out. The Attendance tool's "Signed In" filter and the Photos tool's list both ask this, and
    /// they ask it here rather than each carrying a copy: two screens that disagree about who is
    /// present is a worse bug than either screen being wrong.
    ///
    /// <para>
    /// <b><c>== true</c>, not <c>!= false</c>.</b> <paramref name="records"/> is null both while the
    /// store is loading and when it has failed, and <c>null != false</c> is true — which turns
    /// "nobody is signed in" into "show the entire roster" at exactly the moment the leader cannot
    /// tell the difference.
    /// </para>
    /// </summary>
    public static bool IsSignedIn(IEnumerable<AttendanceRecord>? records, Guid personId) =>
        records?.Any(x => x.PersonId == personId && x.LocalSignedOut == null) == true;
}