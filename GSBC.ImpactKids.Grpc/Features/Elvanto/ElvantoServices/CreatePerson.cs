using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using PeopleMediaConsent = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsent;
using PeopleMediaConsentHelper = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsentHelper;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    private static readonly TimeZoneInfo AestZone =
        TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    private static string? ToAestDate(DateTimeOffset? utc) =>
        utc.HasValue ? TimeZoneInfo.ConvertTime(utc.Value, AestZone).ToString("yyyy-MM-dd") : null;

    /// <summary>
    /// <paramref name="medicalAllergyNotes"/> is composed by MedicalAllergyNotesDescriptor so a
    /// created person carries the same round-trippable text an updated one does. Without it a
    /// create pushed free-text notes only, losing every allergen and condition name, and the
    /// result could not be read back on the next sync.
    /// </summary>
    public async Task<string?> CreatePersonAsync(
        DbPerson          person,
        string?           medicalAllergyNotes = null,
        CancellationToken token               = default)
    {
        ElvantoCreatePersonRequest req = new()
        {
            FirstName             = person.FirstName,
            LastName              = person.LastName,
            Email                 = person.Email,
            Mobile                = person.PhoneNumber,
            Birthday              = ToAestDate(person.DateOfBirth),
            FirstTimeAtImpactKids = ToAestDate(person.FirstTime),
            MediaConsent          = Enum.TryParse<PeopleMediaConsent>(person.MediaConsent, out PeopleMediaConsent mc)
                                        ? PeopleMediaConsentHelper.ToDisplay(mc)
                                        : null,
            MedicalAllergyNotes   = medicalAllergyNotes ?? MergeAllergyAndMedicalNotes(person)
        };

        // Build first, then decide. The payload is the thing worth reviewing, so it is logged
        // whether or not it is sent - that is the whole point of running with writes off.
        if (!WritesEnabled)
        {
            logger.LogWarning(
                "ELVANTO CREATE SUPPRESSED for {FirstName} {LastName} (app person {PersonId}). "
                + "Would POST {Uri} with: {Payload}",
                person.FirstName, person.LastName, person.Id,
                ElvantoCreatePersonRequest.RequestUri, DescribePayload(req));
            return null;
        }

        ElvantoCreatePersonResponse? response =
            await SendMessage<ElvantoCreatePersonRequest, ElvantoCreatePersonResponse>(req, token);

        if (response?.Status != "ok" || response.Person?.Id is null)
        {
            logger.LogWarning(
                "Failed to create person {FirstName} {LastName} in Elvanto: {Error}",
                person.FirstName, person.LastName, response?.Error?.Message ?? "unknown error");
            return null;
        }

        return response.Person.Id;
    }

    // Mirrors AllergiesOutboundDescriptor/MedicalNotesOutboundDescriptor's merge format so a person
    // created with pre-existing allergy/medical records doesn't lose that data until the next sync.
    private static string? MergeAllergyAndMedicalNotes(DbPerson person)
    {
        string? allergies = person.Allergies.Count == 0
            ? null
            : string.Join(", ", person.Allergies.Where(a => !string.IsNullOrWhiteSpace(a.Notes)).Select(a => a.Notes));

        string? medical = person.MedicalNotes.Count == 0
            ? null
            : string.Join(", ", person.MedicalNotes.Where(n => !string.IsNullOrWhiteSpace(n.Notes)).Select(n => n.Notes));

        string? merged = allergies is null ? null : $"Allergies: {allergies}";
        if (medical is not null)
            merged = merged is null ? $"Medical: {medical}" : $"{merged}\nMedical: {medical}";

        return merged;
    }
}
