using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Services;

namespace GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;

public partial class MemorisationEntriesService(
    GsbcDbContext                                      db,
    IEventService<MemorisationEntry>                   eventService,
    IConverter<DbMemorisationEntry, MemorisationEntry> converter
) : IMemorisationEntriesService;