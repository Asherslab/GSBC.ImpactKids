namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Elvanto;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServicePositionsRequest
{
    public Rosters Rosters { get; set; } = Rosters.ImpactKids;
}

public enum Rosters
{
    ImpactKids,
    Production
}