using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using PeopleMediaConsent = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsent;
using PeopleMediaConsentHelper = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsentHelper;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    /// <summary>
    /// The "Visitor" people category. People invented by this app are not members of anything yet,
    /// so they land here until a human moves them. Sits alongside the custom field ids on
    /// <see cref="ElvantoPerson"/> as a constant rather than configuration: it identifies a row in
    /// the church's Elvanto account, the same in every environment that talks to it.
    /// </summary>
    public const string VisitorCategoryId = "ee659afc-9fef-11e2-81e1-b196dc8a2002";

    /// <summary>
    /// Elvanto's sentinel for "make a family for this person". Only ever sent for the first member
    /// of a family with no Elvanto presence; everyone after them is given the id it produced.
    /// </summary>
    public const string NewFamily = "new";

    /// <summary>
    /// Elvanto's family relationship for a person the app knows only as guardian-or-not. A child is
    /// unambiguous. An adult is not: "Primary Contact" is already taken by whoever Elvanto has as
    /// the family's main contact, so a second adult goes in as Spouse. That is a guess about a real
    /// relationship - a grandparent or an aunt would also arrive as Spouse - so it is worth a
    /// human's eye on the first few rather than trust.
    /// </summary>
    private static string FamilyRelationshipFor(DbPerson person) =>
        person.FamilyGuardian ? "Primary Contact" : "Child";

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
    /// <summary>
    /// The exact request a create would send. Public so a run can record the body in the audit
    /// trail before deciding whether to send it, without a second mapping that could drift from
    /// this one - the payload reviewed has to be the payload sent.
    /// </summary>
    public string DescribeCreatePayload(
        DbPerson person,
        string?  medicalAllergyNotes = null,
        string?  elvantoFamilyId     = null) =>
        DescribePayload(BuildCreateRequest(person, medicalAllergyNotes, elvantoFamilyId));

    private static ElvantoCreatePersonRequest BuildCreateRequest(
        DbPerson person,
        string?  medicalAllergyNotes,
        string?  elvantoFamilyId)
    {
        return new ElvantoCreatePersonRequest
        {
            CategoryId            = VisitorCategoryId,
            FamilyId              = elvantoFamilyId,
            // Only sent with a family - a relationship on its own describes nothing.
            FamilyRelationship    = elvantoFamilyId is null ? null : FamilyRelationshipFor(person),
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
    }

    /// <summary>
    /// The person Elvanto created. <paramref name="FamilyId"/> matters when the request asked for
    /// <c>"new"</c>: it is the id of the family that did not exist a moment ago, and the next
    /// member of that family has to be told it rather than asking for "new" all over again.
    /// </summary>
    public record CreatedPerson(string Id, string? FamilyId);

    /// <summary>
    /// Why a create did not happen, in the words Elvanto used. Returned rather than only logged
    /// because the console is unreachable in some environments and tail-truncated in the rest, and
    /// "the create failed" without a reason costs a whole debugging cycle.
    /// </summary>
    public string? LastCreateError { get; private set; }

    public async Task<CreatedPerson?> CreatePersonAsync(
        DbPerson          person,
        string?           medicalAllergyNotes = null,
        string?           elvantoFamilyId     = null,
        CancellationToken token               = default)
    {
        ElvantoCreatePersonRequest req = BuildCreateRequest(person, medicalAllergyNotes, elvantoFamilyId);

        // Build first, then decide. The payload is the thing worth reviewing, so it is logged
        // whether or not it is sent - that is the whole point of running with writes off.
        LastCreateError = null;

        if (!CreatesEnabled)
        {
            logger.LogWarning(
                "ELVANTO CREATE SUPPRESSED for {FirstName} {LastName} (app person {PersonId}). "
                + "Would POST {Uri} with: {Payload}",
                person.FirstName, person.LastName, person.Id,
                ElvantoCreatePersonRequest.RequestUri, DescribePayload(req));
            LastCreateError = "suppressed: creates disabled";
            return null;
        }

        ElvantoCreatePersonResponse? response =
            await SendMessage<ElvantoCreatePersonRequest, ElvantoCreatePersonResponse>(req, token);

        if (response?.Status != "ok" || response.Person?.Id is null)
        {
            // A null response means the transport refused it (writes off, or the budget spent) and
            // nothing was sent; a response with an error means Elvanto rejected the payload. Those
            // are very different findings, so the message says which.
            LastCreateError = response is null
                ? "no response - refused before sending, or the request failed"
                : $"{response.Error?.Type}: {response.Error?.Message ?? "unknown error"} (status={response.Status})";

            logger.LogWarning(
                "Failed to create person {FirstName} {LastName} in Elvanto: {Error}",
                person.FirstName, person.LastName, LastCreateError);
            return null;
        }

        string? newFamilyId = response.Person.FamilyId?.ToString();

        // Asked for a new family and Elvanto did not say which one it made. Read it back rather
        // than shrug: without an id every later sibling asks for "new" and the family fragments
        // into one household per child.
        if (newFamilyId is null && req.FamilyId == NewFamily)
        {
            ElvantoPerson? readBack = await GetPersonInfoAsync(response.Person.Id, token);
            newFamilyId = readBack?.FamilyId;
            logger.LogWarning(
                "Elvanto created person {ElvantoId} into a new family but returned no family_id; "
                + "read back as {FamilyId}", response.Person.Id, newFamilyId ?? "(still unknown)");
        }

        return new CreatedPerson(response.Person.Id, newFamilyId);
    }

    // Mirrors AllergiesOutboundDescriptor/MedicalNotesOutboundDescriptor's merge format so a person
    // created with pre-existing allergy/medical records doesn't lose that data until the next sync.
    //
    // Row count is not the test for "has something to say". A person recorded as having no known
    // allergies still has rows - they point at the "None" allergen and the "None" medical type with
    // no note - so counting them produced the label with nothing after it: "Allergies: \nMedical: ".
    // A create would have written that literal string into Elvanto's medical field. Emptiness has to
    // be judged on the joined text, which is also what makes this agree with
    // MedicalAllergyNotesDescriptor, whose null means "nothing worth pushing" rather than "not
    // supplied".
    private static string? MergeAllergyAndMedicalNotes(DbPerson person)
    {
        string? allergies = Join(person.Allergies.Select(a => a.Notes));
        string? medical   = Join(person.MedicalNotes.Select(n => n.Notes));

        string? merged = allergies is null ? null : $"Allergies: {allergies}";
        if (medical is not null)
            merged = merged is null ? $"Medical: {medical}" : $"{merged}\nMedical: {medical}";

        return merged;

        static string? Join(IEnumerable<string?> notes)
        {
            string joined = string.Join(", ", notes.Where(n => !string.IsNullOrWhiteSpace(n)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
    }
}
