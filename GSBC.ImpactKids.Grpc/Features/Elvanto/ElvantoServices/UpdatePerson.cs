using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    /// <summary>
    /// Outcome of an edit. <paramref name="NewFamilyId"/> is set only when the request asked Elvanto
    /// to create the family, and carries the id it made - the caller needs it so another member
    /// moved into the same family in the same run joins it rather than asking for a second "new".
    /// </summary>
    public record UpdateOutcome(bool Landed, string? NewFamilyId = null);

    public async Task<UpdateOutcome> UpdatePersonAsync(
        ElvantoUpdatePersonRequest request,
        CancellationToken          token = default)
    {
        if (!UpdatesEnabled)
        {
            logger.LogWarning(
                "ELVANTO UPDATE SUPPRESSED for Elvanto person {ElvantoId}. Would POST {Uri} with: {Payload}",
                request.Id, ElvantoUpdatePersonRequest.RequestUri, DescribePayload(request));
            return new UpdateOutcome(false);
        }

        ElvantoMutationResponse? response =
            await SendMessage<ElvantoUpdatePersonRequest, ElvantoMutationResponse>(request, token);

        if (response?.Status != "ok")
        {
            LastUpdateError = response is null
                ? "no response - refused before sending, or the request failed"
                : $"{response.Error?.Type}: {response.Error?.Message ?? "unknown error"} (status={response.Status})";

            logger.LogWarning(
                "Failed to update person {ElvantoId} in Elvanto: {Error}", request.Id, LastUpdateError);
            return new UpdateOutcome(false);
        }

        if (request.FamilyId != NewFamily)
            return new UpdateOutcome(true);

        // Elvanto has just made a family for this person. people/edit does not report which, so read
        // it back - without it a second member moved into the same family this run would ask for
        // another "new" and end up in a household of their own.
        ElvantoPerson? readBack = await GetPersonInfoAsync(request.Id, token);
        logger.LogInformation(
            "Elvanto moved person {ElvantoId} into a newly created family {FamilyId}",
            request.Id, readBack?.FamilyId ?? "(unknown)");

        return new UpdateOutcome(true, readBack?.FamilyId);
    }

    /// <summary>Why the last edit did not happen, in Elvanto's words. See LastCreateError.</summary>
    public string? LastUpdateError { get; private set; }
}
