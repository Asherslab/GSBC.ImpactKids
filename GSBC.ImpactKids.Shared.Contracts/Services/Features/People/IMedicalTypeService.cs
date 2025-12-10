using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.MedicalType")]
public interface IMedicalTypeService
{
    Task<BasicReadMultipleResponse<MedicalType>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
}