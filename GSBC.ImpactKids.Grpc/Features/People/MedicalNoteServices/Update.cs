using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

public partial class MedicalNoteService
{
    public async Task<BasicResponse> Update(UpdateMedicalNoteRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMedicalNote? medicalNote = await db.MedicalNotes
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (medicalNote == null)
            return BasicResponse.WithError(MedicalNoteNotFound);

        if (request.MedicalTypeId.IsUpdated)
        {
            if (request.MedicalTypeId.Value != null)
            {
                DbMedicalType? type = await db.MedicalTypes
                    .FirstOrDefaultAsync(x => x.Id == request.MedicalTypeId.Value, token);

                if (type == null)
                    return BasicResponse.WithError(MedicalTypeNotFound);
            }

            medicalNote.MedicalTypeId = request.MedicalTypeId.Value;
        }

        if (request.Notes.IsUpdated)
        {
            medicalNote.Notes = request.Notes.Value;

            if (string.IsNullOrWhiteSpace(medicalNote.Notes))
                medicalNote.Notes = null;
        }

        if (request.Severe.IsUpdated)
        {
            medicalNote.Severe = request.Severe.Value;
        }

        db.MedicalNotes.Update(medicalNote);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}