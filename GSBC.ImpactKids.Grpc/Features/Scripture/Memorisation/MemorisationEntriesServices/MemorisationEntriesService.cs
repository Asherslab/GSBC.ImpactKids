using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MemorisationEntriesService(
    GsbcDbContext                                             db,
    IEventService<MemorisationEntry>                          eventService,
    IConverter<DbVirtualMemorisationEntry, MemorisationEntry> converter
) : IMemorisationEntriesService;