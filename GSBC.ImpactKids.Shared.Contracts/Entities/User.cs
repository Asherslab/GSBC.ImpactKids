namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record User
{
    public required Guid   Id   { get; init; }
    public required string Name { get; init; }
    
    public required bool Enabled { get; init; }
    
    public static string BuildSubscription(Guid? userId = null) => 
        $"{nameof(User)}.{userId?.ToString() ?? "*"}";
}