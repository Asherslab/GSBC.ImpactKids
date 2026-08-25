using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoConfig
{
    public required string                  Authentication { get; set; }
    public          ElvantoReportResponse[] Reports        { get; set; } = [];

    /// <summary>
    /// Master switch for every write to Elvanto (people/create and people/edit).
    /// Defaults to <c>false</c>, so an environment that says nothing about it cannot push.
    /// Turning it on is the only way a mutation leaves this process - see
    /// <c>ElvantoService.SendMessage</c>, which refuses to hand a mutation to HttpClient
    /// while this is false, whatever the calling code does.
    /// Reads (people/getAll, people/getInfo, services/getAll) are unaffected.
    /// </summary>
    public bool AllowWrites { get; set; }
}