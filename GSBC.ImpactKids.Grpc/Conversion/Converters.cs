using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using Riok.Mapperly.Abstractions;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;
using ContractReviewStatus = GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync.ManualReviewStatus;

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
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbPerson, Person>
{
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
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbService, Service>
{
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
public partial class MemoryVerseConverter : IConverter<DbMemoryVerse, MemoryVerse>
{
    [MapProperty(nameof(DbMemoryVerse.BibleVerses), nameof(MemoryVerse.BibleVerseIds), Use = nameof(MapBibleVerseIds))]
    [MapProperty(nameof(DbMemoryVerse.Services), nameof(MemoryVerse.ServiceIds), Use = nameof(MapServiceIds))]
    public partial MemoryVerse Convert(DbMemoryVerse input);

    ImmutableList<Guid> MapBibleVerseIds(List<DbBibleVerse> bibleVerses)
        => bibleVerses.Select(x => x.Id).ToImmutableList();

    ImmutableList<Guid> MapServiceIds(List<DbService> services)
        => services.Select(x => x.Id).ToImmutableList();
}

[Mapper]
public partial class MemorisationEntryConverter : IConverter<DbMemorisationEntry, MemorisationEntry>
{
    public partial MemorisationEntry Convert(DbMemorisationEntry input);
}

[Mapper]
public partial class AttendanceRecordConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbAttendanceRecord, AttendanceRecord>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    public partial AttendanceRecord Convert(DbAttendanceRecord input);
}

[Mapper]
public partial class AttendanceItemTypeConverter : IConverter<DbAttendanceItemType, AttendanceItemType>
{
    public partial AttendanceItemType Convert(DbAttendanceItemType input);
}

[Mapper]
public partial class AttendanceItemRecordConverter : IConverter<DbAttendanceItemRecord, AttendanceItemRecord>
{
    public partial AttendanceItemRecord Convert(DbAttendanceItemRecord input);
}

[Mapper]
public partial class GameBoardConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbGameBoard, GameBoard>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    public partial GameBoard Convert(DbGameBoard input);
}

[Mapper]
public partial class GamePointRecordConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbGamePointRecord, GamePointRecord>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreTarget(nameof(GamePointRecord.LocalAwarded))]
    [MapperIgnoreTarget(nameof(GamePointRecord.IsBehaviour))]
    public partial GamePointRecord Convert(DbGamePointRecord input);
}

[Mapper]
public partial class SyncOperationConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSyncOperation, SyncOperation>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreSource(nameof(DbSyncOperation.AuditLogs))]
    [MapperIgnoreSource(nameof(DbSyncOperation.Person))]
    public partial SyncOperation Convert(DbSyncOperation input);
}

[Mapper]
public partial class SyncAuditLogConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSyncAuditLog, SyncAuditLog>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreSource(nameof(DbSyncAuditLog.SyncOperation))]
    public partial SyncAuditLog Convert(DbSyncAuditLog input);
}

public class SyncPendingReviewConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSyncPendingReview, SyncManualReviewEntry>
{
    public SyncManualReviewEntry Convert(DbSyncPendingReview review) => new()
    {
        Id              = review.Id,
        PersonId        = review.PersonId,
        ElvantoId       = review.ElvantoId,
        PersonName      = review.PersonName,
        MatchStrategy   = review.MatchStrategy,
        MatchConfidence = review.MatchConfidence,
        Status          = (ContractReviewStatus)(int)review.Status,
        ReviewedAt      = review.ReviewedAt.HasValue ? dateTimeConverter.Convert(review.ReviewedAt.Value) : null,
        CreatedAt       = dateTimeConverter.Convert(review.CreatedAt)
    };
}

public class SyncResultConverter : IConverter<SyncResult, SyncResponse>
{
    public SyncResponse Convert(SyncResult result) => new()
    {
        Success = result.Success,
        Error = result.Error,
        OperationId = result.OperationId.ToString(),
        Mode = result.Mode.ToString(),
        PeopleProcessed = result.PeopleProcessed,
        InboundPeople = result.InboundPeople,
        InboundFields = result.InboundFields,
        OutboundPeople = result.OutboundPeople,
        OutboundFields = result.OutboundFields,
        Conflicts = result.Conflicts,
        AutoLinked = result.AutoLinked,
        ManualReviewQueued = result.ManualReviewQueued,
        Archived = result.Archived,
        ManualReviewItems = result.ManualReviewItems.Select(m => new SyncManualReviewItem
        {
            PersonId = m.PersonId.ToString(),
            ElvantoId = m.ElvantoId,
            Reason = m.Reason,
            MatchConfidence = m.MatchConfidence
        }).ToList()
    };
}
