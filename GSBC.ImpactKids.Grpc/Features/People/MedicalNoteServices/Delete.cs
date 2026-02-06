using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

public partial class MedicalNoteService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMedicalNote? note = await db.MedicalNotes
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (note == null)
            return BasicResponse.WithError(MedicalNoteNotFound);

        db.MedicalNotes.Remove(note);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}