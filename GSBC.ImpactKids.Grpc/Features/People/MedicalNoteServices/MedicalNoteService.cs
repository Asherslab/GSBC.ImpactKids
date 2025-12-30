using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MedicalNoteService(
    GsbcDbContext                          db,
    IEventService<MedicalNote>             eventService,
    IConverter<DbMedicalNote, MedicalNote> converter
) : IMedicalNoteService
{
    private async Task SendEvent(Guid personId, Guid familyId, CancellationToken token = default)
    {
        await eventService.SendUpdatedEvent(personId, token: token, familyId);
    }
}