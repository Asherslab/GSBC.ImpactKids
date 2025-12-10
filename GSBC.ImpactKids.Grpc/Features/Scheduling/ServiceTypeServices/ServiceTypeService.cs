using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class ServiceTypeService(
    GsbcDbContext                          db,
    IEventService<ServiceType>             eventService,
    IConverter<DbServiceType, ServiceType> converter
) : IServiceTypeService;