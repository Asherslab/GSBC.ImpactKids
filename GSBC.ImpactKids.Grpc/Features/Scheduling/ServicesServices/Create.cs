using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.SchoolTermId != null)
        {
            DbSchoolTerm? term =
                await db.Terms.FirstOrDefaultAsync(x => x.Id == request.SchoolTermId, cancellationToken: token);

            if (term == null)
                return BasicReadResponse<Guid?>.WithError(SchoolTermNotFound);
        }

        if (request.ServiceTypeId != null)
        {
            DbServiceType? serviceType =
                await db.ServiceTypes.FirstOrDefaultAsync(x => x.Id == request.ServiceTypeId, cancellationToken: token);

            if (serviceType == null)
                return BasicReadResponse<Guid?>.WithError(ServiceTypeNotFound);
        }

        if (request.Date == default)
            return BasicReadResponse<Guid?>.WithError(ServiceDateNull);

        if (string.IsNullOrWhiteSpace(request.Name))
            request.Name = null;

        DbService service = new()
        {
            Id = Guid.Empty,
            Name = request.Name,

            Date = request.Date,
            SchoolTermId = request.SchoolTermId,
            ServiceTypeId = request.ServiceTypeId
        };

        await db.Services.AddAsync(service, token);

        // A new service starts with its service type's default schedule, if the type has one.
        Guid? defaultTemplateId = request.ServiceTypeId == null
            ? null
            : await db.Schedules
                .Where(x => x.DefaultForServiceTypeId == request.ServiceTypeId && x.ServiceId == null)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(token);

        if (defaultTemplateId != null)
            await ScheduleCopy.StageForService(db, defaultTemplateId.Value, service.Id, token);

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        if (defaultTemplateId != null)
            await eventService.SendUpdatedEvent<Schedule>(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = service.Id,
            Success = true
        };
    }
}