using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Serialization;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedMember.Global

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ServicesResponse
{
    [JsonPropertyName("services")]
    public ElvantoServices? Services { get; set; }
}

public class ElvantoServices
{
    [JsonPropertyName("service")]
    public List<Service> Service { get; set; } = [];
}

public class Service
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("service_type")]
    public ServiceType? ServiceType { get; set; }

    [JsonPropertyName("volunteers")]
    public Volunteers? Volunteers { get; set; }
}

public class ServiceType
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class Volunteers
{
    [JsonPropertyName("plan")]
    public List<Plan>? Plan { get; set; } = [];
}

public class Plan
{
    [JsonPropertyName("positions")]
    public Positions? Positions { get; set; }
}

public class Positions
{
    [JsonPropertyName("position")]
    public List<Position> Position { get; set; } = [];
}

public class Position
{
    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("sub_department_name")]
    public string? SubDepartmentName { get; set; }

    [JsonPropertyName("position_name")]
    public string? PositionName { get; set; }

    [JsonPropertyName("volunteers")]
    [JsonConverter(typeof(NullableStringConverter<PositionVolunteers?>))]
    public PositionVolunteers? Volunteers { get; set; }
}

public class PositionVolunteers
{
    [JsonPropertyName("volunteer")]
    public List<Volunteer> Volunteer { get; set; } = [];
}

public class Volunteer
{
    [JsonPropertyName("person")]
    public ElvantoPerson? Person { get; set; }
}

public class ElvantoPerson
{
    public const string CustomFieldMedicalId      = "fceebdfd-777f-4cde-be09-0b04b2fe68c8";
    public const string CustomFieldMediaConsentId = "196785e4-a63d-48e1-873f-154144ff4c06";
    public const string CustomFieldFirstTimeId    = "d77458f4-5a18-4820-9e59-a0765d25817a";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public string? LastName { get; set; }

    [JsonPropertyName("family_relationship")]
    public string? FamilyRelationship { get; set; }

    [JsonPropertyName("family_id")]
    public string? FamilyId { get; set; }
    
    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    /// <summary>
    /// Exactly "Male", "Female" or "" — no other value appeared across a full roll of 1754 people.
    ///
    /// <b>It has to be named in the <c>Fields</c> array</b> in <c>FetchPageWithRetries</c>. It is not
    /// returned by default, and leaving it out is silent: the key is simply absent, this binds null
    /// for everyone, and the gender sync does nothing while looking healthy. That is not the same
    /// trap as <c>picture</c>, which is returned by default and fails the entire call if named.
    /// </summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }


    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName($"custom_{CustomFieldMedicalId}")]
    public string? MedicalAllergyNotes { get; set; }

    [JsonPropertyName($"custom_{CustomFieldFirstTimeId}")]
    public string? FirstTimeAtImpactKids { get; set; }

    [JsonPropertyName($"custom_{CustomFieldMediaConsentId}")]
    [JsonConverter(typeof(NullableStringConverter<MediaConsent?>))]
    public MediaConsent? MediaConsent { get; set; }

    [JsonPropertyName("school_grade")]
    [JsonConverter(typeof(NullableStringConverter<SchoolGrade?>))]
    public SchoolGrade? SchoolGrade { get; set; }

    /// <summary>
    /// The profile picture URL, and the exact inverse of <see cref="Gender"/>: it is returned by
    /// default and <b>naming it in the requested <c>Fields</c> array fails the entire call</b> with
    /// <c>code 250: A field does not exist (picture)</c> — which downstream reads as an empty roll.
    ///
    /// <para>
    /// Three shapes come back and only one is a real upload: a <c>cdn.elvanto.com.au</c>
    /// default-avatar, a gravatar fallback, and a <c>d2dek0x2lg6bxh.cloudfront.net/.../members/...</c>
    /// URL. Over half of that last group are malformed by Elvanto itself and 403 permanently. Read
    /// only — there is no way to write a picture through the API.
    /// </para>
    /// </summary>
    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    /// <summary>
    /// When Elvanto last changed this person, as "yyyy-MM-dd HH:mm:ss" in UTC (verified against a
    /// known edit). Returned on every people response and cannot be asked for through "fields" -
    /// requesting it by name is rejected as a field that does not exist.
    /// Empty for a person never edited since creation, which is what <see cref="DateAdded"/> covers.
    /// This is the real edit time, so it replaces "when we last polled" when resolving a conflict.
    /// It is per person, not per field: for one field it is an upper bound on when that field moved.
    /// </summary>
    [JsonPropertyName("date_modified")]
    public string? DateModified { get; set; }

    [JsonPropertyName("date_added")]
    public string? DateAdded { get; set; }

    /// <summary>
    /// <see cref="DateModified"/>, falling back to <see cref="DateAdded"/> for a person that has
    /// never been edited. Null when neither parses, which leaves the caller on its old behaviour
    /// rather than inventing a time.
    /// </summary>
    public DateTimeOffset? LastChangedAtUtc =>
        ParseElvantoUtc(DateModified) ?? ParseElvantoUtc(DateAdded);

    private static DateTimeOffset? ParseElvantoUtc(string? value) =>
        DateTime.TryParseExact(value?.Trim(), "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTime parsed)
            ? new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
            : null;
}

/// <summary>
/// A "select" custom field value as Elvanto returns it. The attributes are load-bearing: Elvanto
/// sends lowercase "id"/"name", the response options do not set PropertyNameCaseInsensitive, so
/// without them both properties bound to null. That failed quietly - the object itself was not
/// null, so every read of media consent looked like a deliberate "Not Requested" whatever Elvanto
/// actually held, and no inbound consent change could ever be seen.
/// </summary>
public class MediaConsent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <inheritdoc cref="MediaConsent"/>
public class SchoolGrade
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}