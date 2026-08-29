using System.Collections.Immutable;
using System.Globalization;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;
using MediaConsent = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsent;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

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
                dob = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                if (matchedPerson?.DbPerson.DateOfBirth == null)
                    dateOfBirth = dob;
            }

            if (DateTime.TryParseExact(elvantoPerson.FirstTimeAtImpactKids, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime first))
            {
                first = DateTime.SpecifyKind(first, DateTimeKind.Utc);
                if (matchedPerson?.DbPerson.FirstTime == null)
                    firstTime = first;
            }

            DbPerson person = new()
            {
                Id = Guid.Empty,
                ElvantoId = elvantoPerson.Id,
                FirstName = matchedPerson?.DbPerson.FirstName ?? elvantoPerson.FirstName ?? "Elvanto import error",
                LastName = matchedPerson?.DbPerson.LastName ?? elvantoPerson.LastName ?? "Elvanto import error",

                Email = matchedPerson?.DbPerson.Email ??
                        (string.IsNullOrWhiteSpace(elvantoPerson.Email)
                            ? null
                            : elvantoPerson.Email),
                PhoneNumber = matchedPerson?.DbPerson.PhoneNumber ??
                              (string.IsNullOrWhiteSpace(elvantoPerson.Mobile)
                                  ? string.IsNullOrWhiteSpace(elvantoPerson.Phone)
                                      ? null
                                      : elvantoPerson.Phone
                                  : elvantoPerson.Mobile),

                SchoolGradeId = matchedPerson?.DbPerson.SchoolGradeId ??
                                schoolGrades.FirstOrDefault(x => x.ElvantoId == elvantoPerson.SchoolGrade?.Id)?.Id,
                MediaConsent = matchedPerson?.DbPerson.MediaConsent == nameof(MediaConsent.NotRequested)
                    ? MediaConsentHelper.FromElvanto(elvantoPerson.MediaConsent?.Name).ToString()
                    : matchedPerson?.DbPerson.MediaConsent
                      ?? MediaConsentHelper.FromElvanto(elvantoPerson.MediaConsent?.Name).ToString(),
                // Elvanto's blank is "" here, and it is not an answer - so it never displaces what
                // the app already holds, and never becomes an empty string in the column.
                Gender = matchedPerson?.DbPerson.Gender
                         ?? (string.IsNullOrWhiteSpace(elvantoPerson.Gender)
                                 ? null
                                 : elvantoPerson.Gender),
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
            Entities = people.ToImmutableList()
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


    private const int PageSize = 1000;
    private const int MaxAttemptsPerPage = 3;

    /// <summary>
    /// Fetches every person, or throws. There is deliberately no third outcome: a caller that
    /// receives a list can rely on it being the whole list, because everything downstream -
    /// archiving in particular - treats absence from this list as proof of deletion.
    /// </summary>
    private async Task<List<ElvantoPerson>> RetrieveElvantoPeople(CancellationToken token = default)
    {
        int                 page          = 1;
        int?                expectedTotal = null;
        List<ElvantoPerson> elvantoPeople = [];

        while (true)
        {
            PeopleResponse? resp = await FetchPageWithRetries(page, token);

            // Out of retries. Returning what we have would look identical to a complete fetch.
            if (resp?.People?.Person is null)
                throw new ElvantoFetchException(
                    $"Elvanto people/getAll failed on page {page} after {MaxAttemptsPerPage} attempts. "
                    + $"{elvantoPeople.Count} people had been fetched so far; refusing to treat a partial "
                    + "result as the full roll.");

            // Elvanto reports the authoritative count on every page, so it is checked once and
            // then held to at the end.
            expectedTotal ??= resp.People.Total;

            elvantoPeople.AddRange(resp.People.Person);

            if (!int.TryParse(resp.People.PerPage, out int perPage) || perPage <= 0)
                perPage = PageSize;

            int totalPages = (int)Math.Ceiling(resp.People.Total / (double)perPage);
            if (totalPages <= resp.People.Page) break;

            page++;
        }

        // Belt and braces: pages could each succeed and still not add up, e.g. if the roll
        // changed underneath us mid-fetch. Better a failed sync than a mass archive.
        if (expectedTotal is not null && elvantoPeople.Count < expectedTotal)
            throw new ElvantoFetchException(
                $"Elvanto reported {expectedTotal} people but only {elvantoPeople.Count} were fetched. "
                + "Refusing to continue with an incomplete roll.");

        return elvantoPeople;
    }

    private async Task<PeopleResponse?> FetchPageWithRetries(int page, CancellationToken token)
    {
        for (int attempt = 1; attempt <= MaxAttemptsPerPage; attempt++)
        {
            PeopleResponse? resp = await SendMessage<PeopleRequest, PeopleResponse>(
                new PeopleRequest
                {
                    Suspended = "no",
                    Contact = "no",
                    Archived = "no",
                    Page = page,
                    PageSize = PageSize,
                    Fields =
                    [
                        "school_grade",
                        "birthday",
                        // Must be asked for. It is NOT returned by default - measured against the
                        // live account, a getAll with no "gender" here omits the key entirely,
                        // whether or not a fields array is supplied at all. Leaving it out does not
                        // fail the call, it just makes GenderDescriptor read null for every person
                        // and the field sync silently dead. "picture" is the opposite trap: naming
                        // it here fails the whole call with code 250.
                        "gender",
                        $"custom_{ElvantoPerson.CustomFieldMedicalId}",
                        $"custom_{ElvantoPerson.CustomFieldMediaConsentId}",
                        $"custom_{ElvantoPerson.CustomFieldFirstTimeId}"
                    ]
                },
                token
            );

            if (resp?.People?.Person is not null) return resp;

            if (attempt < MaxAttemptsPerPage)
            {
                // A rate limit or a blip is the usual cause, so back off rather than hammering.
                TimeSpan delay = TimeSpan.FromSeconds(2 * attempt);
                logger.LogWarning(
                    "Elvanto people/getAll page {Page} returned nothing (attempt {Attempt}/{Max}); retrying in {Delay}s",
                    page, attempt, MaxAttemptsPerPage, delay.TotalSeconds);
                await Task.Delay(delay, token);
            }
        }

        return null;
    }
}