using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

[Service("gRPC/GSBC.ImpactKids.Schedules.ItemTemplates")]
public interface IScheduleItemTemplateService
    : IBasicReadMultipleService<ScheduleItemTemplate>,
        ICreateService<CreateScheduleItemTemplateRequest>,
        IUpdateService<UpdateScheduleItemTemplateRequest>,
        IBasicDeleteService<ScheduleItemTemplate>;
