namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record AttendanceItemType : IIdentifiable
{
    public required Guid Id { get; init; }

    public required string Label             { get; init; }
    public required int?   Reward            { get; init; }
    public required bool   RequiresReturning { get; init; }
}