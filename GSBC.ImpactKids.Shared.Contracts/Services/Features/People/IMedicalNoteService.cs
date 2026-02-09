using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("gRPC/GSBC.ImpactKids.Person.MedicalNotes")]
public interface IMedicalNoteService
    : IBasicReadMultipleService<MedicalNote>,
        ICreateService<CreateMedicalNoteRequest>,
        IUpdateService<UpdateMedicalNoteRequest>,
        IBasicDeleteService<MedicalNote>;