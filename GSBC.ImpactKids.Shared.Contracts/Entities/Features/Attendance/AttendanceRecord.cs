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

    public bool Deleted { get; init; }

    public required Guid  PersonId        { get; init; }
    public required Guid  SignedInUserId  { get; init; }
    public          Guid? SignedOutUserId { get; init; }
    public required Guid  ServiceId       { get; init; }
}