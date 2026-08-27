using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreatePersonRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName  { get; set; } = null!;

    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    
    public Guid?        SchoolGradeId { get; set; }
    public MediaConsent MediaConsent  { get; set; } = MediaConsent.NotRequested;
    public DateTime?    DateOfBirth   { get; set; }
    public DateTime?    FirstTime     { get; set; }

    [ProtoIgnore]
    public DateTime? LocalDateOfBirth
    {
        get => DateOfBirth?.ToLocalTime();
        set => DateOfBirth = value?.ToUniversalTime();
    }

    [ProtoIgnore]
    public DateTime? LocalFirstTime
    {
        get => FirstTime?.ToLocalTime();
        set => FirstTime = value?.ToUniversalTime();
    }

    public Guid? FamilyId       { get; set; }
    public bool  FamilyGuardian { get; set; }

    /// <summary>
    /// "Put the new person in <i>this</i> person's family" — used when the family may not exist yet.
    ///
    /// Several hundred people now legitimately have no household (Elvanto reports them as "No
    /// Family"), and "Create Person in Family" has to keep working for them. The family cannot be
    /// minted on the client, because it has to land on <b>both</b> people or neither: a Guid
    /// generated in the browser and applied to only the new person would leave the existing one
    /// behind, and applying it to both takes two calls that can half-fail. The server does it in one
    /// transaction instead.
    ///
    /// Ignored when <see cref="FamilyId"/> is set, so an explicit pick in the family selector — a
    /// real family, or "No family" — always wins over the page you happened to arrive from.
    /// </summary>
    public Guid? FamilyWithPersonId { get; set; }
}