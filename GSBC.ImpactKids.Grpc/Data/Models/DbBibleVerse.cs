using System.ComponentModel.DataAnnotations.Schema;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbBibleVerse
{
    public required Guid Id { get; set; }
    
    public required string Verse         { get; set; }
    
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required int VerseNumber { get; set; }
    
    public required int    ChapterNumber { get; set; }
    public required int    BookNumber    { get; set; }
    public required string BookName      { get; set; }

    [MapperIgnore]
    public List<DbMemoryVerse> MemoryVerses { get; set; } = [];
}