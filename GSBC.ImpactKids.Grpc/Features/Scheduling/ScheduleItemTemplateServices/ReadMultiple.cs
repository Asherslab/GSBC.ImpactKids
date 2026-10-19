using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleItemTemplateServices;

public partial class ScheduleItemTemplateService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<ScheduleItemTemplate>>
        BasicReadMultiple(BasicReadMultipleRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbScheduleItemTemplate> query = db.ScheduleItemTemplates;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
            }
        }

        query = query.OrderBy(x => x.Name);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<ScheduleItemTemplate> response in
                       query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}
