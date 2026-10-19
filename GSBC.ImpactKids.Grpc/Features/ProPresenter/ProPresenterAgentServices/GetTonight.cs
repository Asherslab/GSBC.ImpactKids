using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

public partial class ProPresenterAgentService
{
    /// <summary>
    /// Today's service - today in Sydney - that has a schedule, with what has run so far. The agent
    /// reads it on every schedule change event and works out the stage from it with the same
    /// <c>ScheduleRun</c> rules the phones use. Two services today with schedules is not expected; the
    /// earlier one wins.
    /// </summary>
    public async Task<BasicReadResponse<ProPresenterTonight>> GetTonight(
        ProPresenterAgentRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (CallingMachine(context) == null)
            return BasicReadResponse<ProPresenterTonight>.WithError(PermissionDenied);

        // Today's bounds as Sydney sees them, at offset zero - Npgsql writes no other offset.
        DateTime       today    = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SydneyZone).Date;
        DateTimeOffset dayStart = new DateTimeOffset(today, SydneyZone.GetUtcOffset(today)).ToUniversalTime();
        DateTimeOffset dayEnd   = dayStart.AddDays(1);

        DbService? service = await db.Services
            .AsNoTracking()
            .Where(x => x.Date >= dayStart && x.Date < dayEnd)
            .Where(x => db.Schedules.Any(s => s.ServiceId == x.Id))
            .OrderBy(x => x.Date)
            .FirstOrDefaultAsync(token);

        if (service == null)
            return new BasicReadResponse<ProPresenterTonight> { Success = true, Entity = null };

        DbSchedule schedule = await db.WholeSchedules()
            .AsNoTracking()
            .FirstAsync(x => x.ServiceId == service.Id, token);

        return new BasicReadResponse<ProPresenterTonight>
        {
            Success = true,
            Entity = new ProPresenterTonight
            {
                Schedule = scheduleConverter.Convert(schedule),
                ServiceName = service.Name ?? today.ToString("dd/MM/yyyy"),
                ServiceDate = today
            }
        };
    }
}
