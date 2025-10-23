using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

public partial class ElvantoService
{
    private static readonly string[] SchoolGrades =
    [
        "Nursery/Pre-school",
        "Kindergarten",
        "Prep",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6"
    ];

    public async Task<BasicReadMultipleResponse<DbPerson>> GetImpactKidsAgePeople(CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        List<DbPerson> people = [];
        foreach (string schoolGrade in SchoolGrades)
        {
            PeopleResponse? resp = await SendMessage<PeopleRequest, PeopleResponse>(
                new PeopleRequest
                {
                    PageSize = 1000,
                    SearchObject = new SearchObject
                    {
                        SchoolGrade = schoolGrade
                    },
                    Fields = ["school_grade"]
                },
                token
            );

            if (resp?.People?.Person == null) continue;

            people.AddRange(
                resp.People.Person.Select(person => new DbPerson
                    {
                        Id = Guid.Empty,
                        ElvantoId = person.Id,
                        FirstName = person.FirstName ?? "",
                        LastName = person.LastName ?? "",
                        PreferredName = person.PreferredName ?? ""
                    }
                )
            );
        }

        return new BasicReadMultipleResponse<DbPerson>
        {
            Success = true,
            Entities = people
        };
    }
}