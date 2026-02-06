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
}

public class MediaConsent
{
    public string? Id   { get; set; }
    public string? Name { get; set; }
}

public class SchoolGrade
{
    public string? Id   { get; set; }
    public string? Name { get; set; }
}