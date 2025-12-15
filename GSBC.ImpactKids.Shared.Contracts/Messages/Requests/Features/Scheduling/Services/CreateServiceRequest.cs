using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateServiceRequest
{
    public string? Name { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    
    [ProtoIgnore]
    public DateTime LocalDate
    {
        get => Date.ToLocalTime();
        set => Date = value.ToUniversalTime();
    }

    // Used by frontend only.
    [ProtoIgnore]
    public SchoolTerm? SchoolTerm
    {
        get;
        set
        {
            SchoolTermId = value?.Id;
            field = value;
        }
    }

    public Guid? SchoolTermId  { get; set; }
    
    // Used by frontend only.
    [ProtoIgnore]
    public ServiceType? ServiceType
    {
        get;
        set
        {
            ServiceTypeId = value?.Id;
            field = value;
        }
    }
    
    public Guid? ServiceTypeId { get; set; }
}