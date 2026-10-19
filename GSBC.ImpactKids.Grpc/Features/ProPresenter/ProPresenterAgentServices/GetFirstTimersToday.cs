using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

public partial class ProPresenterAgentService
{
    /// <summary>
    /// Children here for the first time today: <b>signed in today</b> and with a <b>first time date
    /// of today</b>, both read as Sydney dates.
    /// <para>
    /// Both halves are needed. The first-time date alone would welcome a child registered today who
    /// never came; signed-in alone cannot tell a newcomer from a regular. And the date is only half
    /// trustworthy on its own - it comes from Elvanto for anyone who attended before this app existed.
    /// </para>
    /// <para>
    /// <b>Today's bounds are one Sydney day, converted to UTC before they reach the query</b> - Npgsql
    /// refuses a non-zero offset (see the gRPC AGENTS.md, "Dates"). The same window works for
    /// <c>FirstTime</c> however it was written: the Elvanto sync stores the date as midnight UTC, the
    /// person form as local midnight, and both fall inside the same Sydney day.
    /// </para>
    /// </summary>
    public async Task<BasicReadMultipleResponse<FirstTimer>> GetFirstTimersToday(
        ProPresenterAgentRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (CallingMachine(context) == null)
            return new BasicReadMultipleResponse<FirstTimer> { Success = false, Error = PermissionDenied };

        DateTime       sydneyToday = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SydneyZone).Date;
        DateTimeOffset dayStart    = new DateTimeOffset(sydneyToday, SydneyZone.GetUtcOffset(sydneyToday)).ToUniversalTime();
        DateTime       tomorrow    = sydneyToday.AddDays(1);
        DateTimeOffset dayEnd      = new DateTimeOffset(tomorrow, SydneyZone.GetUtcOffset(tomorrow)).ToUniversalTime();

        List<FirstTimer> firstTimers = await db.People
            .AsNoTracking()
            .Where(x => x.DeletedAtUtc == null && x.FirstTime >= dayStart && x.FirstTime < dayEnd)
            .Where(x => db.AttendanceRecords.Any(a =>
                a.PersonId == x.Id && !a.Deleted && a.SignedIn >= dayStart && a.SignedIn < dayEnd))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new FirstTimer
            {
                PersonId = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName
            })
            .ToListAsync(token);

        return new BasicReadMultipleResponse<FirstTimer>
        {
            Success = true,
            Entities = firstTimers.ToImmutableList()
        };
    }
}
