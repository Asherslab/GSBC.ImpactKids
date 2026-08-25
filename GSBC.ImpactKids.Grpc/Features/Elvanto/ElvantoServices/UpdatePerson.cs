using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    public async Task<bool> UpdatePersonAsync(ElvantoUpdatePersonRequest request, CancellationToken token = default)
    {
        if (!WritesEnabled)
        {
            logger.LogWarning(
                "ELVANTO UPDATE SUPPRESSED for Elvanto person {ElvantoId}. Would POST {Uri} with: {Payload}",
                request.Id, ElvantoUpdatePersonRequest.RequestUri, DescribePayload(request));
            return false;
        }

        ElvantoMutationResponse? response =
            await SendMessage<ElvantoUpdatePersonRequest, ElvantoMutationResponse>(request, token);

        if (response?.Status != "ok")
        {
            logger.LogWarning(
                "Failed to update person {ElvantoId} in Elvanto: {Error}",
                request.Id, response?.Error?.Message ?? "unknown error");
            return false;
        }

        return true;
    }
}
