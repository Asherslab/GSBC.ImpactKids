using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

public partial class MedicalNoteService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<MedicalNote>> BasicReadMultiple(
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

        await foreach (BasicReadMultipleResponse<MedicalNote> response in
                       query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}