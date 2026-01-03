using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMedicalNoteRequest : ReadRequestBase, IUpdateRequest<MedicalNote, UpdateMedicalNoteRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<Guid?> MedicalTypeId { get; set; } = new();

    public DeltaUpdate<string?> Notes  { get; set; } = new();
    public DeltaUpdate<bool>    Severe { get; set; } = new();

    public static UpdateMedicalNoteRequest FromEntity(MedicalNote entity)
    {
        UpdateMedicalNoteRequest request = new()
        {
            Guid = entity.Id
        };

        request.MedicalTypeId.SetInitialValue(entity.MedicalTypeId);

        request.Notes.SetInitialValue(entity.Notes);
        request.Severe.SetInitialValue(entity.Severe);
        return request;
    }
}