using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Sync;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService(
    GsbcDbContext                                          db,
    IConverter<DbSyncOperation, SyncOperation>             operationConverter,
    IConverter<DbSyncAuditLog, SyncAuditLog>               auditLogConverter,
    IConverter<DbSyncPendingReview, SyncManualReviewEntry> pendingReviewConverter,
    IConverter<DbSyncPlannedChange, SyncPlannedChange>     plannedChangeConverter,
    IConverter<SyncResult, SyncResponse>                   syncResultConverter,
    IElvantoPersonSyncService                              syncEngine,
    IEventService<SyncOperation>                           eventService,
    IEventService<SyncManualReviewEntry>                   manualReviewEntryEventService,
    IEventService<Person>                                  personEventService
) : ISyncService;