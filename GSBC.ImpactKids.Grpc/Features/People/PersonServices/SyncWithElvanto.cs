using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicResponse> SyncWithElvanto(CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        BasicReadMultipleResponse<DbPerson> resp = await elvantoService
            .GetPeople(token);

        ICollection<string> existingPeopleElvantoIds = await UpdateExistingPeople(resp, token);
        await CreateNewPeople(resp, existingPeopleElvantoIds, token);

        await eventService.SendUpdatedEvent(Guid.Empty, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }

    private async Task<ICollection<string>> UpdateExistingPeople(
        BasicReadMultipleResponse<DbPerson> resp,
        CancellationToken                   token = default
    )
    {
        List<string> matchedElvantoPeople = [];
        await foreach (DbPerson dbPerson in db.People)
        {
            DbPerson? elvantoPerson = resp.Entities.FirstOrDefault(x => x.ElvantoId == dbPerson.ElvantoId);

            if (elvantoPerson == null || dbPerson.ElvantoId == null)
                continue;

            matchedElvantoPeople.Add(dbPerson.ElvantoId);
            if (dbPerson.FirstName != elvantoPerson.FirstName)
                dbPerson.FirstName = elvantoPerson.FirstName;
            if (dbPerson.LastName != elvantoPerson.LastName)
                dbPerson.LastName = elvantoPerson.LastName;
            if (dbPerson.SchoolGradeId != elvantoPerson.SchoolGradeId)
                dbPerson.SchoolGradeId = elvantoPerson.SchoolGradeId;
            if (dbPerson.MediaConsent != elvantoPerson.MediaConsent)
                dbPerson.MediaConsent = elvantoPerson.MediaConsent;

            if (elvantoPerson.DateOfBirth != null && dbPerson.DateOfBirth != elvantoPerson.DateOfBirth)
                dbPerson.DateOfBirth = elvantoPerson.DateOfBirth;
            if (elvantoPerson.FirstTime != null && dbPerson.FirstTime != elvantoPerson.FirstTime)
                dbPerson.FirstTime = elvantoPerson.FirstTime;

            if (dbPerson.FamilyId != elvantoPerson.FamilyId)
                dbPerson.FamilyId = elvantoPerson.FamilyId;
            if (dbPerson.FamilyGuardian != elvantoPerson.FamilyGuardian)
                dbPerson.FamilyGuardian = elvantoPerson.FamilyGuardian;

            if (dbPerson.Allergies.Count == 0 &&
                dbPerson.MedicalNotes.Count == 0)
            {
                if (elvantoPerson.MedicalNotes.Count != 0)
                    dbPerson.MedicalNotes = elvantoPerson.MedicalNotes;

                if (elvantoPerson.Allergies.Count != 0)
                    dbPerson.Allergies = elvantoPerson.Allergies;
            }

            db.People.Update(dbPerson);
        }

        await db.SaveChangesAsync(token);
        return matchedElvantoPeople;
    }

    private async Task CreateNewPeople(
        BasicReadMultipleResponse<DbPerson> resp,
        ICollection<string>                 existingPeopleElvantoIds,
        CancellationToken                   token = default
    )
    {
        foreach (DbPerson respEntity in resp.Entities
                     .Where(x => x.ElvantoId != null && !existingPeopleElvantoIds.Contains(x.ElvantoId))
                )
        {
            await db.People.AddAsync(respEntity, token);
        }

        await db.SaveChangesAsync(token);
    }
}