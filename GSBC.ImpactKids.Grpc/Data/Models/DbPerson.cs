using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbPerson
{
    public required Guid    Id        { get; set; }
    [MapperIgnore]
    public          string? ElvantoId { get; set; }
    
    public required string  FirstName     { get; set; }
    public required string  LastName      { get; set; }
    public          string? PreferredName { get; set; }
}