using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Services;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVersesServices;

public partial class MemoryVersesService(
    GsbcDbContext              db,
    IEventService<MemoryVerse> eventService,
    IConverter<DbMemoryVerse, MemoryVerse> converter
) : IMemoryVersesService;