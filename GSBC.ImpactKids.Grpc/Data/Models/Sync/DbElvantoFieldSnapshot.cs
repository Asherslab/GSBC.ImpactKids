namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbElvantoFieldSnapshot
{
    public required Guid           Id           { get; set; }
    public required string         EntityType   { get; set; }
    public required Guid           EntityId     { get; set; }
    public required string         FieldName    { get; set; }
    public required string         LastSeenHash { get; set; }
    public          string?        LastSeenValue { get; set; }
    public required DateTimeOffset LastSeenAt   { get; set; }
}
