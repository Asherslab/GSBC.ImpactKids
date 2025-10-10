namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Service
{
    public required Guid    Id   { get; set; }
    public          string? Name { get; set; }

    public required DateTime Date { get; set; }

    public required Guid SchoolTermId { get; set; }
}