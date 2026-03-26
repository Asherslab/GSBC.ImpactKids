namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServicePositionsRequest
{
    public Rosters   Rosters { get; set; } = Rosters.ImpactKids;
    
    public DateTime? StartDate   { get; set; }
    public DateTime? EndDate { get; set; }
}

public enum Rosters
{
    ImpactKids,
    Production
}