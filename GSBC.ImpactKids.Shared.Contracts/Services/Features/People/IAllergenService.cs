using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.Allergen")]
public interface IAllergenService
{
    Task<BasicReadMultipleResponse<Allergen>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
}