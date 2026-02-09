using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("gRPC/GSBC.ImpactKids.Person.Allergies")]
public interface IAllergyService
    : IBasicReadMultipleService<Allergy>,
        ICreateService<CreateAllergyRequest>,
        IUpdateService<UpdateAllergyRequest>,
        IBasicDeleteService<Allergy>;