using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<BasicResponse> Create(CreateMemorisationEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        Task<bool> personFoundTask = dbFactory.RunWithNewDbContext(
            temp => temp.People.AnyAsync(y => y.Id == request.PersonId, token),
            token
        );

        Task<bool> memoryVerseFoundTask = dbFactory.RunWithNewDbContext(
            temp => temp.MemoryVerses.AnyAsync(x => x.Id == request.MemoryVerseId, token),
            token
        );

        Task<bool> serviceFoundTask = dbFactory.RunWithNewDbContext(
            temp => temp.Services.AnyAsync(x => x.Id == request.ServiceId, token),
            token
        );

        await Task.WhenAll(
            personFoundTask,
            memoryVerseFoundTask,
            serviceFoundTask
        );

        if (!personFoundTask.Result)
            return BasicResponse.WithError(PersonNotFound);

        if (!memoryVerseFoundTask.Result)
            return BasicResponse.WithError(MemoryVerseNotFound);

        if (!serviceFoundTask.Result)
            return BasicResponse.WithError(ServiceNotFound);

        DbMemorisationEntry entry = new()
        {
            Id = Guid.Empty,

            PersonId = request.PersonId,
            MemoryVerseId = request.MemoryVerseId,
            ServiceId = request.ServiceId,

            VerseRecited = request.VerseRecited,
            FiveDollaryDoosGiven = request.FiveDollaryDoosGiven,
            OneDollaryDooGiven = request.OneDollaryDooGiven
        };

        await db.MemorisationEntries.AddAsync(entry, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}