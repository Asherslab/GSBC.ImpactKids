using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DataDisplay;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService(
    GsbcDbContext                              db,
    IConfiguration                             configuration,
    IEventService<MetabaseEmbed>               eventService,
    IConverter<DbMetabaseEmbed, MetabaseEmbed> converter
) : IMetabaseService;
