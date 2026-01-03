using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

public partial class MedicalNoteService
{
    public async Task<BasicResponse> Create(CreateMedicalNoteRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.MedicalTypeId == null && string.IsNullOrWhiteSpace(request.Notes))
            return BasicResponse.WithError(MedicalNotesMustHaveTypeOrNotes);

        DbPerson? person = await db.People.FirstOrDefaultAsync(x => x.Id == request.PersonId, token);

        if (person == null)
            return BasicResponse.WithError(PersonNotFound);

        if (request.MedicalTypeId != null)
        {
            bool medicalTypeExists = await db.MedicalTypes.AnyAsync(x => x.Id == request.MedicalTypeId, token);

            if (!medicalTypeExists)
                return BasicResponse.WithError(MedicalTypeNotFound);
        }

        DbMedicalNote medicalNote = new()
        {
            Id = Guid.Empty,
            PersonId = request.PersonId,
            MedicalTypeId = request.MedicalTypeId,
            Notes = request.Notes,
            Severe = request.Severe
        };

        await db.MedicalNotes.AddAsync(medicalNote, token);
        await db.SaveChangesAsync(token);
        await SendEvent(person.Id, person.FamilyId, token);

        return new BasicResponse
        {
            Success = true
        };
    }
}