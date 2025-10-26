using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<MemoryVerseAnalyticsResponse?> RecitationsPerVerseAnalytics(
        MemorisationEntriesAnalyticsRequest request,
        CallContext                         context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .Include(x => x.MemoryVerses)
            .ThenInclude(x => x.Services)
            .Include(x => x.MemoryVerses)
            .ThenInclude(x => x.MemorisationEntries)
            .FirstOrDefaultAsync(x => x.Id == request.MemoryVerseListId, token);

        if (list == null)
            return new MemoryVerseAnalyticsResponse
            {
                Error = MemoryVerseListNotFound,
                Success = false
            };

        List<DbService> services = list.MemoryVerses
            .SelectMany(x => x.Services)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Date)
            .ToList();

        List<MemoryVerseVerticalAxis> verticalAxis = [];
        foreach (DbMemoryVerse verse in list.MemoryVerses.OrderByDescending(x => x.Services.Count))
        {
            List<double> dataPoints = [];
            foreach (DbService service in services)
            {
                dataPoints.Add(verse.MemorisationEntries!.Count(x => x.ServiceId == service.Id && x.VerseRecited));
            }

            verticalAxis.Add(new MemoryVerseVerticalAxis
            {
                Label = verse.ReferenceName,
                DataPoints = dataPoints.ToArray()
            });
        }

        return new MemoryVerseAnalyticsResponse
        {
            Success = true,

            XAxisLabels = services.Select(x => x.Date.ToString("dd/MM")).ToList(),
            VerticalAxis = verticalAxis
        };
    }
}