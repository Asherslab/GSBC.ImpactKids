using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleItemTemplateServices;

public partial class ScheduleItemTemplateService(
    GsbcDbContext                                            db,
    IEventService<ScheduleItemTemplate>                      eventService,
    IConverter<DbScheduleItemTemplate, ScheduleItemTemplate> converter
) : IScheduleItemTemplateService;
