using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("gRPC/GSBC.ImpactKids.Person.Allergen")]
public interface IAllergenService : IBasicReadMultipleService<Allergen>;