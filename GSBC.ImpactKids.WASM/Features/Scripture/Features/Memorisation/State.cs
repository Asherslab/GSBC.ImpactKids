using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation;

public record MemorisationToolState(
    Guid  ServiceId,
    bool  Previous,
    bool  Upcoming,
    Guid? MemoryVerseId
) : IInitialisableState<MemorisationToolState>
{
    public static MemorisationToolState Initial => new(
        Guid.Empty,
        false,
        true,
        null
    );

    public MemorisationToolState SetServiceId(Guid      serviceId)     => this with { ServiceId = serviceId };
    public MemorisationToolState SetPrevious(bool       previous)      => this with { Previous = previous };
    public MemorisationToolState SetUpcoming(bool       upcoming)      => this with { Upcoming = upcoming };
    public MemorisationToolState SetMemoryVerseId(Guid? memoryVerseId) => this with { MemoryVerseId = memoryVerseId };
}