using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

public partial class MedicalNoteService
{
    public async Task<BasicReadMultipleResponse<MedicalNote>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMedicalNote> query = db.MedicalNotes;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.MedicalType!.Label.ToLower().Contains(search.ToLower()) ||
                    x.Notes!.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.OrderBy(x => x.Person!.Id);

        query = query.Paginate(request);

        List<DbMedicalNote> types = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<MedicalNote>
        {
            Success = true,
            Entities = types.Select(converter.Convert).ToImmutableList()
        };
    }
}