using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicReadResponse<MemoryVerseList>> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (list == null)
            return BasicReadResponse<MemoryVerseList>.WithError(MemoryVerseListNotFound);

        return new BasicReadResponse<MemoryVerseList>
        {
            Success = true,
            Entity = converter.Convert(list)
        };
    }
}