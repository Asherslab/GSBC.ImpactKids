namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateServiceRequest
{
    public string?  Name { get; set; }
    public DateTime Date { get; set; } = DateTime.Now.Date.ToUniversalTime();
    
    [ProtoIgnore]
    public DateTime LocalDate
    {
        get => Date.ToLocalTime();
        set => Date = value.ToUniversalTime();
    }

    public Guid? SchoolTermId  { get; set; }
    
    public Guid? ServiceTypeId { get; set; }
}