namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Person : IIdentifiable
{
    public required Guid Id { get; init; }

    public required string FirstName { get; init; }
    public required string LastName  { get; init; }

    public required string? PhoneNumber { get; init; }
    public required string? Email       { get; init; }

    public required Guid?        SchoolGradeId { get; init; }
    public required MediaConsent MediaConsent  { get; init; }
    public required DateTime?    DateOfBirth   { get; init; }
    public required DateTime?    FirstTime     { get; init; }

    /// <summary>
    /// Male, Female, or null for "we have not been told".
    ///
    /// Null is the third state and it is a real answer, which is why this is a nullable enum rather
    /// than carrying an <c>Unknown</c> member: an <c>Unknown</c> reads as a value, and a value is
    /// something code will eventually push to Elvanto as though someone had chosen it. About a third
    /// of children arrive from Elvanto with this blank, so the null case is the common one.
    /// </summary>
    public required Gender? Gender { get; init; }

    [ProtoIgnore]
    public DateTime? LocalDateOfBirth => DateOfBirth?.ToLocalTime();

    [ProtoIgnore]
    public DateTime? LocalFirstTime => FirstTime?.ToLocalTime();

    // family stuff
    public required Guid FamilyId       { get; init; }
    public required bool FamilyGuardian { get; init; }

    public string? ElvantoId { get; init; }

    /// <summary>
    /// Whether this person is in a household at all.
    ///
    /// <see cref="Guid.Empty"/> means they are not, and it is a real answer rather than missing data:
    /// Elvanto reports several hundred people as "No Family" and the sync records exactly that. It
    /// must never be treated as a family id — every such person shares the one value, so grouping on
    /// it produces a single "family" of everybody who has none, named after whichever surname is
    /// commonest among them. That is what the app's old "no family yet" bucket did, and it showed a
    /// woman her family as "Kent (412)" with 411 strangers in it.
    /// </summary>
    [ProtoIgnore]
    public bool HasFamily => FamilyId != Guid.Empty;

    /// <summary>
    /// Whether these two are in the same household. False when either has none — "neither is in a
    /// family" is not a family in common.
    /// </summary>
    public bool SharesFamilyWith(Person other) => HasFamily && FamilyId == other.FamilyId;

    /// <summary>
    /// The household's name, taken from its commonest surname, or the person's own when they have
    /// no household.
    ///
    /// Also the one place the empty group is handled: <c>MaxBy</c> returns null for an empty
    /// sequence, and the three call sites all dereferenced it, so a person absent from the supplied
    /// list threw rather than falling back.
    /// </summary>
    public static string FamilyNameOf(Person person, IEnumerable<Person> people) =>
        person.HasFamily
            ? people.Where(x => x.SharesFamilyWith(person))
                    .GroupBy(y => y.LastName)
                    .MaxBy(g => g.Count())?.Key ?? person.LastName
            : person.LastName;

    public int? GetAge() => CalculateAge(LocalDateOfBirth);

    /// <summary>
    /// Age today from a local date of birth. Static so a form can show the age of a date
    /// still being typed, before there is a person to ask.
    /// </summary>
    public static int? CalculateAge(DateTime? localDateOfBirth) => localDateOfBirth == null
        ? null
        : (int.Parse(DateTime.Now.ToString("yyyyMMdd")) - int.Parse(localDateOfBirth.Value.ToString("yyyyMMdd")))
          / 10000;

    public string GetDisplayName() => $"{FirstName} {LastName}";

    public static string BuildSubscription(Guid? familyId = null, Guid? personId = null) =>
        $"{nameof(Person)}.{familyId?.ToString() ?? "*"}.{personId?.ToString() ?? "*"}";
}

/// <summary>
/// The two values Elvanto returns. It has no others — measured across a full roll, <c>gender</c> is
/// exactly "Male", "Female" or "" — so there is nothing to add, and "not told" is the absence of
/// this enum rather than a member of it. See <see cref="Person.Gender"/>.
/// </summary>
[ProtoContract]
public enum Gender
{
    Male,
    Female
}

[ProtoContract]
public enum MediaConsent
{
    NotRequested,
    Yes,
    No,
    StrictlyNo
}

public static class MediaConsentHelper
{
    public static MediaConsent FromElvanto(string? name)
    {
        return name switch
        {
            "Not Requested" => MediaConsent.NotRequested,
            "Yes"           => MediaConsent.Yes,
            "No"            => MediaConsent.No,
            "Strictly No"   => MediaConsent.StrictlyNo,
            _               => MediaConsent.NotRequested
        };
    }

    public static string ToDisplay(this MediaConsent consent)
    {
        return consent switch
        {
            MediaConsent.NotRequested => "Not Requested",
            MediaConsent.Yes          => "Yes",
            MediaConsent.No           => "No",
            MediaConsent.StrictlyNo   => "Strictly No",
            _                         => "Unknown"
        };
    }
}