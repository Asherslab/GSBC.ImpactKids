using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicReadResponse<MemoryVerse>> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;
        
        DbMemoryVerse? verse = await db.MemoryVerses
            .Include(x => x.BibleVerses)
            .Include(x => x.Services)
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (verse == null)
            return BasicReadResponse<MemoryVerse>.WithError(MemoryVerseNotFound);

        return new BasicReadResponse<MemoryVerse>
        {
            Success = true,
            Entity = converter.Convert(verse)
        };
    }
}