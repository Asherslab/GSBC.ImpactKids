using System.Net.Http.Headers;
using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Elvanto;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService(
    GsbcDbContext           db,
    HttpClient              httpClient,
    ElvantoConfig           config,
    ElvantoWriteBudget      budget,
    ILogger<ElvantoService> logger
) : IElvantoService
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Whether writes to Elvanto are permitted. Callers use this to report what they *would*
    /// have done instead of reporting a failure.
    /// </summary>
    public bool WritesEnabled => config.AllowWrites;

    /// <summary>
    /// Whether a create or an update can actually reach Elvanto. Callers must ask these rather than
    /// <see cref="WritesEnabled"/> before reporting what they did: with writes on but one endpoint
    /// off, the transport still refuses, and an audit row saying "Pushed" for a refused write is
    /// worse than no row at all.
    /// </summary>
    public bool CreatesEnabled => config.AllowWrites && config.AllowCreates;

    /// <inheritdoc cref="CreatesEnabled"/>
    public bool UpdatesEnabled => config.AllowWrites && config.AllowUpdates;

    /// <summary>
    /// Whether this app person may be created in Elvanto. True for everyone when no allow list is
    /// configured. The transport enforces the same idea bluntly through the write budget; this is
    /// the readable half, so the audit trail can say which people were deliberately left out.
    /// </summary>
    public bool MayCreate(Guid appPersonId) =>
        config.AllowedCreatePersonIds.Length == 0 || config.AllowedCreatePersonIds.Contains(appPersonId);

    /// <inheritdoc cref="MayCreate"/>
    public bool MayUpdate(Guid appPersonId) =>
        config.AllowedUpdatePersonIds.Length == 0 || config.AllowedUpdatePersonIds.Contains(appPersonId);

    /// <summary>
    /// The exact JSON body that would be POSTed for this request. Serialized with the same
    /// options JsonContent.Create uses, so what gets logged is what would go on the wire -
    /// null-valued fields omitted included.
    /// </summary>
    internal static string DescribePayload<TRequest>(TRequest request) =>
        System.Text.Json.JsonSerializer.Serialize(request);

    /// <summary>
    /// Why this mutation may not be sent, or null if it may. Every check sits above the line that
    /// touches HttpClient, so a caller that forgets its own checks still cannot get a write out.
    /// The budget is consumed last, and only once the request is otherwise cleared, so a refused
    /// write never spends the allowance.
    /// </summary>
    private string? RefuseMutation(ElvantoMutation kind)
    {
        if (!config.AllowWrites) return "Elvanto:AllowWrites=false";

        switch (kind)
        {
            case ElvantoMutation.Create when !config.AllowCreates: return "Elvanto:AllowCreates=false";
            case ElvantoMutation.Update when !config.AllowUpdates: return "Elvanto:AllowUpdates=false";
        }

        return budget.TryConsume()
            ? null
            : $"Elvanto:MaxWrites={budget.MaxWrites} already spent ({budget.Used})";
    }

    private async Task<TResponse?> SendMessage<TRequest, TResponse>(TRequest request, CancellationToken token = default)
        where TRequest : IRequestMessage
    {
        // The single gate every Elvanto call passes through. Callers are expected to check
        // WritesEnabled and log their own context first, but this is what actually makes a
        // push impossible: nothing below this line runs for a mutation while writes are off,
        // so a new caller that forgets the check still cannot reach the network.
        if (TRequest.Mutation != ElvantoMutation.None)
        {
            string? refusal = RefuseMutation(TRequest.Mutation);
            if (refusal is not null)
            {
                logger.LogWarning(
                    "ELVANTO WRITE BLOCKED ({Reason}). Nothing was sent to {Uri}. "
                    + "Payload that would have been sent: {Payload}",
                    refusal, TRequest.RequestUri, DescribePayload(request));
                return default;
            }

            logger.LogWarning(
                "ELVANTO WRITE ALLOWED ({Kind}) to {Uri}. Budget {Used}/{Max}. Payload: {Payload}",
                TRequest.Mutation, TRequest.RequestUri, budget.Used,
                budget.MaxWrites?.ToString() ?? "unlimited", DescribePayload(request));
        }

        HttpRequestMessage httpRequest = new(HttpMethod.Post, TRequest.RequestUri);
        string             encoded     = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.Authentication));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        httpRequest.Content = JsonContent.Create(request);

        try
        {
            HttpResponseMessage message  = await httpClient.SendAsync(httpRequest, token);
            string              rawBody  = await message.Content.ReadAsStringAsync(token);
            TResponse?          response = System.Text.Json.JsonSerializer.Deserialize<TResponse>(rawBody, _jsonOptions);

            if (response is PeopleResponse)
                logger.LogWarning("Elvanto {Uri} returned null after deserialization. Raw body: {Body}",
                    TRequest.RequestUri, rawBody);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Elvanto {Uri}: request failed", TRequest.RequestUri);
            return default;
        }
    }
}