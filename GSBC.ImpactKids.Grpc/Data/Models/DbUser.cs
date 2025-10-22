using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbUser
{
    public required Guid   Id        { get; set; }
    [MapperIgnore]
    public required string GoogleSub { get; set; }
    public required string Name      { get; set; }
    
    public bool Enabled { get; set; }
}