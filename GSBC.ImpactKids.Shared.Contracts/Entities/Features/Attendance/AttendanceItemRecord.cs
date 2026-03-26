namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record AttendanceItemRecord : IIdentifiable
{
    public required Guid Id { get; init; }

    public required bool  RewardGiven  { get; init; }
    public required bool? ItemReturned { get; init; }

    public string? Notes { get; init; }

    public required Guid  AttendanceRecordId   { get; init; }
    public required Guid? AttendanceItemTypeId { get; init; }
}