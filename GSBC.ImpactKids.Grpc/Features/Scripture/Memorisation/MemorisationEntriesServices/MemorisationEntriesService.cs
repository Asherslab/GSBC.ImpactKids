using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MemorisationEntriesService(
    GsbcDbContext                                      db,
    IDbContextFactory<GsbcDbContext>              dbFactory,
    IEventService<MemorisationEntry>                   eventService,
    IConverter<DbMemorisationEntry, MemorisationEntry> converter
) : IMemorisationEntriesService;