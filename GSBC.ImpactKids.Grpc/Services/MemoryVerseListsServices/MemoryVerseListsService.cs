using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVerseListsServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MemoryVerseListsService(
    GsbcDbContext                                  db,
    IEventService<MemoryVerseList>                 eventService,
    IConverter<DbMemoryVerseList, MemoryVerseList> converter
) : IMemoryVerseListsService;