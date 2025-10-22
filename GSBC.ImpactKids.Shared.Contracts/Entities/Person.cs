namespace GSBC.ImpactKids.Shared.Contracts.Entities;

public class Person
{
    public required Guid Id { get; set; }

    public required string  FirstName     { get; set; }
    public required string  LastName      { get; set; }
    public          string? PreferredName { get; set; }

    public string GetDisplayName() => PreferredName ?? $"{FirstName} {LastName}";

    public static string BuildSubscription(Guid? personId = null) =>
        $"{nameof(Person)}.{personId?.ToString() ?? "*"}";
}