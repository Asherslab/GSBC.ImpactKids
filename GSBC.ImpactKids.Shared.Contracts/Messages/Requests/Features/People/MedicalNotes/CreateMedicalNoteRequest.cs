namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMedicalNoteRequest
{
    public Guid PersonId { get; set; }

    public Guid? MedicalTypeId { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }
}