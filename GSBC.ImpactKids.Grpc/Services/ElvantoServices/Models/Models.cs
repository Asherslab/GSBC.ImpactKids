using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Serialization;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;

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
    public string Date { get; set; }

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
    public List<Plan> Plan { get; set; } = [];
}

public class Plan
{
    [JsonPropertyName("positions")]
    public Positions? Positions { get; set; }
}

public class Positions
{
    [JsonPropertyName("position")]
    public List<Position> Position { get; set; }
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
    public Person? Person { get; set; }
}

public class Person
{
    [JsonPropertyName("firstname")]
    public string FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public string LastName { get; set; }
}