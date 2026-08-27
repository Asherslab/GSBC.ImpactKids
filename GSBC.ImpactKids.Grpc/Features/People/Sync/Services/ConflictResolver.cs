using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public class ConflictResolver : IConflictResolver
{
    public ConflictResolution Resolve(
        string            fieldName,
        string?           appValue,
        DateTimeOffset?   appChangedAt,
        string?           elvantoValue,
        DateTimeOffset?   elvantoChangedAt,
        PrecedenceOnTie   precedenceOnTie
    )
    {
        // Both timestamps present and unequal → last-write-wins (intentional clears included)
        if (appChangedAt.HasValue && elvantoChangedAt.HasValue && appChangedAt != elvantoChangedAt)
        {
            bool appIsNewer = appChangedAt > elvantoChangedAt;
            return appIsNewer
                ? new ConflictResolution(SyncSource.App,     appValue,     "LastWriteWins:AppNewer")
                : new ConflictResolution(SyncSource.Elvanto, elvantoValue, "LastWriteWins:ElvantoNewer");
        }

        // Timestamps tied or missing — prefer whichever side actually has a value
        bool appHasValue = !string.IsNullOrWhiteSpace(appValue);
        bool elvHasValue = !string.IsNullOrWhiteSpace(elvantoValue);
        if (appHasValue && !elvHasValue)
            return new ConflictResolution(SyncSource.App,     appValue,     "NonNullWins:AppHasValue");
        if (elvHasValue && !appHasValue)
            return new ConflictResolution(SyncSource.Elvanto, elvantoValue, "NonNullWins:ElvantoHasValue");

        // Both have values (or both null) and timestamps don't decide → use configured precedence
        return precedenceOnTie == PrecedenceOnTie.App
            ? new ConflictResolution(SyncSource.App,     appValue,     "PrecedenceOnTie:App")
            : new ConflictResolution(SyncSource.Elvanto, elvantoValue, "PrecedenceOnTie:Elvanto");
    }
}
