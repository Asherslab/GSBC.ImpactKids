using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class ServicesService(
    GsbcDbContext                  db,
    IEventService<Service>         eventService,
    IConverter<DbService, Service> converter
) : IServicesService;