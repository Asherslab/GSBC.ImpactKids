using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.MedicalNotes")]
public interface IMedicalNoteService
{
    Task<BasicResponse?> Create(
        CreateMedicalNoteRequest request,
        CallContext         context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}