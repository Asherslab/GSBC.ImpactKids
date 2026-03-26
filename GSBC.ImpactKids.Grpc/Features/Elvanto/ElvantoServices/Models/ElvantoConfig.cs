using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoConfig
{
    public required string                  Authentication { get; set; }
    public          ElvantoReportResponse[] Reports        { get; set; } = [];
}