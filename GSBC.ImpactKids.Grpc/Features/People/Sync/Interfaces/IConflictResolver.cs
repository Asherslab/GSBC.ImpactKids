using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IConflictResolver
{
    /// <summary>
    /// Decides which value wins when both App and Elvanto have changed a field.
    /// Returns the winning source and the value to apply.
    /// </summary>
    ConflictResolution Resolve(
        string            fieldName,
        string?           appValue,
        DateTimeOffset?   appChangedAt,
        string?           elvantoValue,
        DateTimeOffset?   elvantoChangedAt,
        PrecedenceOnTie   precedenceOnTie
    );
}
