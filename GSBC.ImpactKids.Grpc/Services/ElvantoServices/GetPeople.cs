using System.Globalization;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;
using MediaConsent = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsent;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices;

public partial class ElvantoService
{
    // private static readonly string[] SchoolGrades =
    // [
    //     "Nursery/Pre-school",
    //     "Kindergarten",
    //     "Prep",
    //     "1",
    //     "2",
    //     "3",
    //     "4",
    //     "5",
    //     "6"
    // ];

    private record Person(
        ElvantoPerson ElvantoPerson,
        DbPerson      DbPerson
    );

    public async Task<BasicReadMultipleResponse<DbPerson>> GetPeople(CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        List<ElvantoPerson> elvantoPeople = await RetrieveElvantoPeople(token);

        List<DbPerson> dbPeople = await db.People
            .ToListAsync(token);

        List<DbSchoolGrade> schoolGrades = await db.SchoolGrades
            .ToListAsync(token);

        List<Person> matchingPeople = elvantoPeople
            .Where(x => dbPeople.Any(y => y.ElvantoId == x.Id))
            .Select(x => new Person(x, dbPeople.First(y => y.ElvantoId == x.Id)))
            .ToList();

        Dictionary<string, Guid> familyIds = elvantoPeople
            .Where(x => x.FamilyId != null)
            .Select(x => x.FamilyId)
            .Distinct()
            .ToDictionary(x => x!,
                x => matchingPeople.FirstOrDefault(y => y.ElvantoPerson.FamilyId == x)?.DbPerson.FamilyId == Guid.Empty
                    ? Guid.NewGuid()
                    : matchingPeople.FirstOrDefault(y => y.ElvantoPerson.FamilyId == x)?.DbPerson.FamilyId ??
                      Guid.NewGuid()
            );

        Guid? nonMedicalId  = (await db.MedicalTypes.FirstOrDefaultAsync(x => x.Label == "None", token))?.Id;
        Guid? nonAllergenId = (await db.Allergens.FirstOrDefaultAsync(x => x.Label == "None", token))?.Id;

        List<DbPerson> people = [];
        foreach (ElvantoPerson elvantoPerson in elvantoPeople)
        {
            Person? matchedPerson = matchingPeople.FirstOrDefault(x => x.ElvantoPerson.Id == elvantoPerson.Id);

            DateTime? dateOfBirth = null;
            DateTime? firstTime   = null;

            if (DateTime.TryParseExact(elvantoPerson.Birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime dob))
            {
                if (matchedPerson?.DbPerson.DateOfBirth == null)
                    dateOfBirth = dob;
            }

            if (DateTime.TryParseExact(elvantoPerson.FirstTimeAtImpactKids, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime first))
            {
                if (matchedPerson?.DbPerson.FirstTime == null)
                    firstTime = first;
            }

            DbPerson person = new()
            {
                Id = Guid.Empty,
                ElvantoId = elvantoPerson.Id,
                FirstName = matchedPerson?.DbPerson.FirstName ?? elvantoPerson.FirstName ?? "Elvanto import error",
                LastName = matchedPerson?.DbPerson.LastName ?? elvantoPerson.LastName ?? "Elvanto import error",

                SchoolGradeId = matchedPerson?.DbPerson.SchoolGradeId ??
                                schoolGrades.FirstOrDefault(x => x.ElvantoId == elvantoPerson.SchoolGrade?.Id)?.Id,
                MediaConsent = matchedPerson?.DbPerson.MediaConsent == nameof(MediaConsent.NotRequested)
                    ? MediaConsentHelper.FromElvanto(elvantoPerson.MediaConsent?.Name).ToString()
                    : matchedPerson?.DbPerson.MediaConsent
                      ?? MediaConsentHelper.FromElvanto(elvantoPerson.MediaConsent?.Name).ToString(),
                DateOfBirth = dateOfBirth,
                FirstTime = firstTime,

                FamilyId = elvantoPerson.FamilyId == null ? Guid.NewGuid() : familyIds[elvantoPerson.FamilyId],
                FamilyGuardian = matchedPerson?.DbPerson.FamilyGuardian ?? IsGuardian(elvantoPerson.FamilyRelationship)
            };

            if (!string.IsNullOrWhiteSpace(elvantoPerson.MedicalAllergyNotes))
            {
                if (matchedPerson == null ||
                    (
                        matchedPerson.DbPerson.Allergies.Count == 0 &&
                        matchedPerson.DbPerson.MedicalNotes.Count == 0
                    )
                   )
                {
                    if (
                        !elvantoPerson.MedicalAllergyNotes.StartsWith("None",
                            StringComparison.InvariantCultureIgnoreCase) &&
                        !elvantoPerson.MedicalAllergyNotes.StartsWith("Nil",
                            StringComparison.InvariantCultureIgnoreCase)
                    )
                    {
                        person.MedicalNotes.Add(new DbMedicalNote
                        {
                            Id = Guid.Empty,
                            MedicalTypeId = null,
                            PersonId = Guid.Empty,
                            Notes = elvantoPerson.MedicalAllergyNotes
                        });
                    }
                    else
                    {
                        person.MedicalNotes.Add(new DbMedicalNote
                        {
                            Id = Guid.Empty,
                            MedicalTypeId = nonMedicalId,
                            PersonId = Guid.Empty,
                            Notes = null
                        });
                        person.Allergies.Add(new DbAllergy
                        {
                            Id = Guid.Empty,
                            AllergenId = nonAllergenId,
                            PersonId = Guid.Empty,
                            Notes = null
                        });
                    }
                }
            }

            people.Add(person);
        }

        return new BasicReadMultipleResponse<DbPerson>
        {
            Success = true,
            Entities = people
        };
    }

    private bool IsGuardian(string? familyRelationship)
    {
        return familyRelationship switch
        {
            "Primary Contact" => true,
            "Spouse"          => true,
            "Partner"         => true,

            _ => false
        };
    }


    private async Task<List<ElvantoPerson>> RetrieveElvantoPeople(CancellationToken token = default)
    {
        int                 page          = 1;
        bool                hasNextPage   = true;
        int                 perPage       = 1000;
        List<ElvantoPerson> elvantoPeople = [];
        while (hasNextPage)
        {
            PeopleResponse? resp = await SendMessage<PeopleRequest, PeopleResponse>(
                new PeopleRequest
                {
                    Suspended = "no",
                    Contact = "no",
                    Archived = "no",
                    Page = page,
                    PageSize = perPage,
                    Fields =
                    [
                        "school_grade",
                        "birthday",
                        $"custom_{ElvantoPerson.CustomFieldMedicalId}",
                        $"custom_{ElvantoPerson.CustomFieldMediaConsentId}",
                        $"custom_{ElvantoPerson.CustomFieldFirstTimeId}"
                    ]
                },
                token
            );

            if (resp?.People?.Person == null)
            {
                hasNextPage = false;
                continue;
            }

            elvantoPeople.AddRange(resp.People.Person);

            int totalPages = (int)Math.Ceiling(resp.People.Total / double.Parse(resp.People.PerPage ?? "0"));
            if (totalPages <= resp.People.Page)
            {
                hasNextPage = false;
            }
            else
            {
                page++;
            }
        }

        return elvantoPeople;
    }
}