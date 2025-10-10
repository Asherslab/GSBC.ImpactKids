namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTerm
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public required DateTime StartDate { get; set; }
    public required DateTime EndDate   { get; set; }

    public List<Service> Services { get; set; } = [];
}