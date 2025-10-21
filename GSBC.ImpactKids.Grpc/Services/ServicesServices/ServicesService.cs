using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

[Authorize]
public partial class ServicesService(
    GsbcDbContext                  db,
    IEventService<Service>         eventService,
    IConverter<DbService, Service> converter
) : IServicesService;