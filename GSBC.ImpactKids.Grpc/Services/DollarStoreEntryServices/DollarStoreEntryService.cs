using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.DollarStoreEntryServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class DollarStoreEntryService(
    GsbcDbContext                                    db,
    IEventService<DollarStoreEntry>                  eventService,
    IConverter<DbDollarStoreEntry, DollarStoreEntry> converter
) : IDollarStoreEntryService;