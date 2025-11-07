using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.MedicalType")]
public interface IMedicalTypeService
{
    Task<BasicReadMultipleResponse<MedicalType>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
}