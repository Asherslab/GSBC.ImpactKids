namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MemorisationEntry
{
    public required Guid Id { get; set; }

    public required Guid PersonId      { get; init; }
    public required Guid MemoryVerseId { get; init; }
    public required Guid ServiceId     { get; init; }

    public bool VerseRecited         { get; init; }
    public bool FiveDollaryDoosGiven { get; init; }
    public bool OneDollaryDooGiven   { get; init; }

    public static string BuildSubscription(
        Guid? personId      = null,
        Guid? serviceId     = null,
        Guid? memoryVerseId = null
    ) =>
        $"{nameof(MemorisationEntry)}." +
        $"{serviceId?.ToString() ?? "*"}." +
        $"{memoryVerseId?.ToString() ?? "*"}." +
        $"{personId?.ToString() ?? "*"}";
}