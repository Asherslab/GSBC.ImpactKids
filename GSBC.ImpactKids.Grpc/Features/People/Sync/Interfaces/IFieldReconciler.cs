using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IFieldReconciler
{
    /// <summary>
    /// Decides what should happen to one field. A pure function of its arguments — no database, no
    /// HTTP, no clock — which is what makes the whole state machine testable.
    /// </summary>
    FieldDecision Decide(
        IFieldSyncDescriptor descriptor,
        FieldComparison      comparison,
        DbSyncFieldConfig    config);
}
