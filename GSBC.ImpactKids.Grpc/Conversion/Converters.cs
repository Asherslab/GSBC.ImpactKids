using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Conversion;

// ReSharper disable UnusedType.Global
public class DateTimeConverter : IConverter<DateTimeOffset, DateTime>
{
    public DateTime Convert(DateTimeOffset input)
    {
        return input.UtcDateTime;
    }
}

[Mapper]
public partial class UserConverter : IConverter<DbUser, User>
{
    public partial User Convert(DbUser user);
}

[Mapper]
public partial class PersonConverter(
    IConverter<DbSchoolGrade, SchoolGrade> schoolGradeConverter,
    IConverter<DbMedicalNote, MedicalNote> medicalNoteConverter,
    IConverter<DbAllergy, Allergy>         allergyConverter,
    IConverter<DateTimeOffset, DateTime>   dateTimeConverter
) : IConverter<DbPerson, Person>
{
    [UseMapper]
    private readonly IConverter<DbSchoolGrade, SchoolGrade> _schoolGradeConverter = schoolGradeConverter;

    [UseMapper]
    private readonly IConverter<DbMedicalNote, MedicalNote> _medicalNoteConverter = medicalNoteConverter;

    [UseMapper]
    private readonly IConverter<DbAllergy, Allergy> _allergyConverter = allergyConverter;

    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreTarget(nameof(Person.LocalDateOfBirth))]
    [MapperIgnoreTarget(nameof(Person.LocalFirstTime))]
    public partial Person Convert(DbPerson person);
}

[Mapper]
public partial class SchoolGradeConverter : IConverter<DbSchoolGrade, SchoolGrade>
{
    public partial SchoolGrade Convert(DbSchoolGrade person);
}

[Mapper]
public partial class MedicalTypeConverter : IConverter<DbMedicalType, MedicalType>
{
    public partial MedicalType Convert(DbMedicalType person);
}

[Mapper]
public partial class MedicalNoteConverter : IConverter<DbMedicalNote, MedicalNote>
{
    public partial MedicalNote Convert(DbMedicalNote note);
}

[Mapper]
public partial class AllergenConverter : IConverter<DbAllergen, Allergen>
{
    public partial Allergen Convert(DbAllergen person);
}

[Mapper]
public partial class AllergyConverter : IConverter<DbAllergy, Allergy>
{
    public partial Allergy Convert(DbAllergy note);
}

[Mapper]
public partial class SchoolTermConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSchoolTerm, SchoolTerm>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreTarget(nameof(SchoolTerm.LocalStartDate))]
    [MapperIgnoreTarget(nameof(SchoolTerm.LocalEndDate))]
    public partial SchoolTerm Convert(DbSchoolTerm input);
}

[Mapper]
public partial class ServiceConverter(
    IConverter<DbSchoolTerm, SchoolTerm>             schoolTermConverter,
    IConverter<DbServiceType, ServiceType>           serviceTypeConverter,
    IConverter<DbDollarStoreEntry, DollarStoreEntry> dollarStoreEntryConverter,
    IConverter<DateTimeOffset, DateTime>             dateTimeConverter
) : IConverter<DbService, Service>
{
    [UseMapper]
    private readonly IConverter<DbSchoolTerm, SchoolTerm> _schoolTermConverter = schoolTermConverter;

    [UseMapper]
    private readonly IConverter<DbServiceType, ServiceType> _serviceTypeConverter = serviceTypeConverter;

    [UseMapper]
    private readonly IConverter<DbDollarStoreEntry, DollarStoreEntry> _dollarStoreEntryConverter =
        dollarStoreEntryConverter;

    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreTarget(nameof(Service.LocalDate))]
    public partial Service Convert(DbService input);
}

[Mapper]
public partial class ServiceTypeConverter : IConverter<DbServiceType, ServiceType>
{
    public partial ServiceType Convert(DbServiceType input);
}

[Mapper]
public partial class DollarStoreEntryConverter : IConverter<DbDollarStoreEntry, DollarStoreEntry>
{
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

    ImmutableList<Guid> MapBibleVerseIds(List<DbBibleVerse> bibleVerses)
        => bibleVerses.Select(x => x.Id).ToImmutableList();

    ImmutableList<Guid> MapServiceIds(List<DbService> services)
        => services.Select(x => x.Id).ToImmutableList();
}

[Mapper]
public partial class VirtualMemorisationEntryConverter(
    IConverter<DbPerson, Person>           personConverter,
    IConverter<DbMemoryVerse, MemoryVerse> memoryVerseConverter,
    IConverter<DbService, Service>         serviceConverter
) : IConverter<DbVirtualMemorisationEntry, MemorisationEntry>
{
    [UseMapper]
    private readonly IConverter<DbPerson, Person> _personConverter = personConverter;

    [UseMapper]
    private readonly IConverter<DbMemoryVerse, MemoryVerse> _memoryVerseConverter = memoryVerseConverter;

    [UseMapper]
    private readonly IConverter<DbService, Service> _serviceConverter = serviceConverter;

    public partial MemorisationEntry Convert(DbVirtualMemorisationEntry input);
}