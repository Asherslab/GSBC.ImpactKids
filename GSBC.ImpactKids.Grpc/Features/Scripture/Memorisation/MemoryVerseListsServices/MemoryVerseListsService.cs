using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MemoryVerseListsService(
    GsbcDbContext                                  db,
    IEventService<MemoryVerseList>                 eventService,
    IConverter<DbMemoryVerseList, MemoryVerseList> converter
) : IMemoryVerseListsService;