namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateServiceTypeRequest
{
    public string? Label { get; set; }

    public string? Color { get; set; }
}