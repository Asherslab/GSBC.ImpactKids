using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Conversion;

// ReSharper disable UnusedType.Global
[Mapper]
public partial class UserConverter : IConverter<DbUser, User>
{
    public partial User Convert(DbUser user);
}

[Mapper]
public partial class PersonConverter : IConverter<DbPerson, Person>
{
    public partial Person Convert(DbPerson person);
}

[Mapper]
public partial class SchoolTermConverter : IConverter<DbSchoolTerm, SchoolTerm>
{
    public partial SchoolTerm Convert(DbSchoolTerm input);
}

[Mapper]
public partial class ServiceConverter : IConverter<DbService, Service>
{
    public partial Service Convert(DbService input);
}

[Mapper]
public partial class DollarStoreEntryConverter(
    IConverter<DbService, Service> serviceConverter
) : IConverter<DbDollarStoreEntry, DollarStoreEntry>
{
    [UseMapper]
    private readonly IConverter<DbService, Service> _serviceConverter = serviceConverter;

    public partial DollarStoreEntry Convert(DbDollarStoreEntry input);
}

[Mapper]
public partial class BibleVerseConverter : IConverter<DbBibleVerse, BibleVerse>
{
    public partial BibleVerse Convert(DbBibleVerse input);
}

[Mapper]
public partial class MemoryVerseListConverter : IConverter<DbMemoryVerseList, MemoryVerseList>
{
    public partial MemoryVerseList Convert(DbMemoryVerseList input);
}

[Mapper]
public partial class MemoryVerseConverter(
    IConverter<DbBibleVerse, BibleVerse> bibleVerseConverter,
    IConverter<DbService, Service>       serviceConverter
) : IConverter<DbMemoryVerse, MemoryVerse>
{
    [UseMapper]
    private readonly IConverter<DbBibleVerse, BibleVerse> _bibleVerseConverter = bibleVerseConverter;

    [UseMapper]
    private readonly IConverter<DbService, Service> _serviceConverter = serviceConverter;

    [MapProperty(nameof(DbMemoryVerse.BibleVerses), nameof(MemoryVerse.BibleVerseIds), Use = nameof(MapBibleVerseIds))]
    [MapProperty(nameof(DbMemoryVerse.Services), nameof(MemoryVerse.ServiceIds), Use = nameof(MapServiceIds))]
    public partial MemoryVerse Convert(DbMemoryVerse input);

    List<Guid> MapBibleVerseIds(List<DbBibleVerse> bibleVerses)
        => bibleVerses.Select(x => x.Id).ToList();

    List<Guid> MapServiceIds(List<DbService> services)
        => services.Select(x => x.Id).ToList();
}

[Mapper]
public partial class MemorisationEntryConverter(
    IConverter<DbPerson, Person>           personConverter,
    IConverter<DbMemoryVerse, MemoryVerse> memoryVerseConverter,
    IConverter<DbService, Service>         serviceConverter
) : IConverter<DbMemorisationEntry, MemorisationEntry>
{
    [UseMapper]
    private readonly IConverter<DbPerson, Person> _personConverter = personConverter;

    [UseMapper]
    private readonly IConverter<DbMemoryVerse, MemoryVerse> _memoryVerseConverter = memoryVerseConverter;

    [UseMapper]
    private readonly IConverter<DbService, Service> _serviceConverter = serviceConverter;

    [MapperIgnoreTarget(nameof(MemorisationEntry.VerseHasBeenRecitedBefore))]
    public partial MemorisationEntry Convert(DbMemorisationEntry input);
}