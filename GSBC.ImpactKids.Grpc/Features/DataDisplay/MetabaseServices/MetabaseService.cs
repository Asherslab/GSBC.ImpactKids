using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DataDisplay;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService(
    GsbcDbContext                              db,
    IConfiguration                             configuration,
    IEventService<MetabaseEmbed>               eventService,
    IConverter<DbMetabaseEmbed, MetabaseEmbed> converter
) : IMetabaseService
{
    /// <summary>The entries of one folder, or of the top of the menu when null, tracked and in menu order.</summary>
    private Task<List<DbMetabaseEmbed>> Siblings(Guid? parentId, CancellationToken token) =>
        db.MetabaseEmbeds
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync(token);

    /// <summary>Numbers a list 0, 1, 2... in its order. Tracked rows, so only the orders that changed are written.</summary>
    private static void Renumber(List<DbMetabaseEmbed> siblings)
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].SortOrder != i)
                siblings[i].SortOrder = i;
        }
    }
}
