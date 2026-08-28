using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    /// <summary>
    /// Open to wall displays as well as leaders. The pickup wall joins these to attendance
    /// records itself rather than being served a purpose built list, which is why there is no
    /// display-shaped contract in this service any more.
    /// <para>
    /// This is the read that carries the most: a display that can call it can see everything a
    /// Person carries. That was accepted knowingly - the enrolment key is held by the owner
    /// and the screens it is put on are ones he controls - and the control that matters is the
    /// key, not this response. See <c>docs/modules/auth/sign-in.md</c>.
    /// </para>
    /// <para>
    /// Opened to displays in <c>Program.cs</c>, not by an attribute here - see
    /// <see cref="Features.Authentication.DisplayAuth.DisplayEndpointExtensions"/> for why an
    /// attribute on this method would be silently ignored.
    /// </para>
    /// </summary>
    public async IAsyncEnumerable<BasicReadMultipleResponse<Person>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbPerson> query = db.People;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query
            .OrderBy(x => x.SchoolGrade!.OrderNumber)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<Person> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}