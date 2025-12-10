namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateServiceRequest
{
    public string? Name { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    public Guid? SchoolTermId  { get; set; }
    public Guid? ServiceTypeId { get; set; }
}