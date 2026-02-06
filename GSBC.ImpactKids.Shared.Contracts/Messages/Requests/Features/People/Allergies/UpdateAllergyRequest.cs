using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateAllergyRequest : ReadRequestBase, IUpdateRequest<Allergy, UpdateAllergyRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<Guid?> AllergenId { get; set; } = new();

    public DeltaUpdate<string?> Notes  { get; set; } = new();
    public DeltaUpdate<bool>    Severe { get; set; } = new();

    public static UpdateAllergyRequest FromEntity(Allergy entity)
    {
        UpdateAllergyRequest request = new()
        {
            Guid = entity.Id
        };

        request.AllergenId.SetInitialValue(entity.AllergenId);

        request.Notes.SetInitialValue(entity.Notes);
        request.Severe.SetInitialValue(entity.Severe);
        return request;
    }
}