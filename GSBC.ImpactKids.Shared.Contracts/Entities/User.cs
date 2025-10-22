namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class User
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }
    
    public required bool Enabled { get; set; }
    
    public static string BuildSubscription(Guid? userId = null) => 
        $"{nameof(User)}.{userId?.ToString() ?? "*"}";
}