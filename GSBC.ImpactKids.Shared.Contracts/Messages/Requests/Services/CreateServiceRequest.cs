namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateServiceRequest
{
    public Guid    SchoolTermId { get; set; }
    public string? Name         { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;
}