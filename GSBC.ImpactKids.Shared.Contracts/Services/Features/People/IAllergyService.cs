using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.Allergies")]
public interface IAllergyService
{
    Task<BasicResponse?> Create(
        CreateAllergyRequest request,
        CallContext         context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}