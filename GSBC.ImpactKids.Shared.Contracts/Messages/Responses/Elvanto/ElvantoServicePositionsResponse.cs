using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ElvantoServicePositionsResponse : ISuccessResponse, IErrorResponse
{
    public List<string>                 Services  { get; set; } = [];
    public List<ElvantoServicePosition> Positions { get; set; } = [];

    public required bool    Success { get; set; }
    public          string? Error   { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ElvantoServicePosition
{
    public required string                     Name                { get; set; }
    public required Dictionary<string, string> PositionsForService { get; set; }
}