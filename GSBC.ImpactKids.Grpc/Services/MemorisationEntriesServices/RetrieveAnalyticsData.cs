using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<MemoryVerseAnalyticsResponse?> RetrieveAnalyticsData(
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

        List<DbService> services = [];

        List<MemoryVerseVerticalAxis> verticalAxis = [];
        foreach (DbMemoryVerse verse in list.MemoryVerses.OrderByDescending(x => x.Services.Count))
        {
            services.AddRange(verse.Services);

            List<double> dataPoints = [];
            foreach (DbService service in verse.Services.OrderBy(x => x.Date))
            {
                dataPoints.Add(verse.MemorisationEntries!.Count(x => x.ServiceId == service.Id && x.VerseRecited));
            }
            
            verticalAxis.Add(new MemoryVerseVerticalAxis
            {
                Verse = memoryVerseConverter.Convert(verse),
                DataPoints = dataPoints.ToArray()
            });
        }
        services = services.OrderBy(x => x.Date).DistinctBy(x => x.Id).ToList();

        return new MemoryVerseAnalyticsResponse
        {
            Success = true,

            Services = services.Select(serviceConverter.Convert).ToList(),
            VerticalAxis = verticalAxis
        };
    }
}