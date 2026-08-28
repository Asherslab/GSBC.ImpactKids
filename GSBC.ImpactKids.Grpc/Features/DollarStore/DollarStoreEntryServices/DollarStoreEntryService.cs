using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DollarStore;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService(
    GsbcDbContext                                    db,
    IEventService<DollarStoreEntry>                  eventService,
    IConverter<DbDollarStoreEntry, DollarStoreEntry> converter
) : IDollarStoreEntryService;