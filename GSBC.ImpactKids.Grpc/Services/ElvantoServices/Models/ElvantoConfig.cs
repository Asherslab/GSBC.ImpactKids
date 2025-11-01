using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Elvanto;

namespace GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;

public class ElvantoConfig
{
    public required string                  Authentication { get; set; }
    public          ElvantoReportResponse[] Reports        { get; set; } = [];
}