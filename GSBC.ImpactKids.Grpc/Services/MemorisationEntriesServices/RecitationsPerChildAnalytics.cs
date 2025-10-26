using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<MemoryVerseAnalyticsResponse?> RecitationsPerChildAnalytics(
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

        foreach (DbService service in services)
        {
            List<DbMemorisationEntry> entriesForThisService = list.MemoryVerses
                .SelectMany(x => x.MemorisationEntries!)
                .Where(x => x.ServiceId == service.Id)
                .ToList();

            List<Guid> peopleWhoRecited = entriesForThisService
                .Where(x => x.VerseRecited)
                .Select(x => x.PersonId)
                .Distinct()
                .ToList();

            foreach (int verseCount in Enumerable.Range(1, list.MemoryVerses.Count))
            {
                int howManyPeopleRecitedThisManyExactly = 0;
                foreach (Guid personId in peopleWhoRecited)
                {
                    if (entriesForThisService.Count(x => x.VerseRecited && x.PersonId == personId) == verseCount)
                    {
                        howManyPeopleRecitedThisManyExactly++;
                    }
                }

                MemoryVerseVerticalAxis? axis = verticalAxis.FirstOrDefault(x => x.Label == verseCount.ToString());
                if (axis == null)
                {
                    verticalAxis.Add(new MemoryVerseVerticalAxis
                    {
                        Label = verseCount.ToString(),
                        DataPoints = [howManyPeopleRecitedThisManyExactly]
                    });
                }
                else
                {
                    axis.DataPoints = axis.DataPoints.Append(howManyPeopleRecitedThisManyExactly).ToArray();
                }
            }
        }

        return new MemoryVerseAnalyticsResponse
        {
            Success = true,

            XAxisLabels = services.Select(x => $"{x.Date:dd/MM} [{list.MemoryVerses.Count(y => y.Services.Any(z => z.Id == x.Id))}]").ToList(),
            VerticalAxis = verticalAxis
        };
    }
}