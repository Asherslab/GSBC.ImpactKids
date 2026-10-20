using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
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
public partial class ScheduleItemTemplateConverter : IConverter<DbScheduleItemTemplate, ScheduleItemTemplate>
{
    public partial ScheduleItemTemplate Convert(DbScheduleItemTemplate input);
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
    [MapperIgnoreSource(nameof(DbSyncOperation.PlannedChanges))]
    // Not a column: the read fills it in, because "does this operation still have work waiting?" is
    // the whole question the Execute button asks.
    [MapperIgnoreTarget(nameof(SyncOperation.PendingPlanItems))]
    public partial SyncOperation Convert(DbSyncOperation input);
}

[Mapper]
public partial class SyncPlannedChangeConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSyncPlannedChange, SyncPlannedChange>
{
    [UseMapper]
    private readonly IConverter<DateTimeOffset, DateTime> _dateTimeConverter = dateTimeConverter;

    [MapperIgnoreSource(nameof(DbSyncPlannedChange.SyncOperation))]
    [MapperIgnoreSource(nameof(DbSyncPlannedChange.Person))]
    [MapperIgnoreSource(nameof(DbSyncPlannedChange.ObservedAppHash))]
    [MapperIgnoreSource(nameof(DbSyncPlannedChange.ObservedElvantoHash))]
    public partial SyncPlannedChange Convert(DbSyncPlannedChange input);
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
        PeopleProcessed = result.PeopleProcessed,
        InboundPeople = result.InboundPeople,
        InboundFields = result.InboundFields,
        OutboundPeople = result.OutboundPeople,
        OutboundFields = result.OutboundFields,
        Conflicts = result.Conflicts,
        AutoLinked = result.AutoLinked,
        ManualReviewQueued = result.ManualReviewQueued,
        Archived = result.Archived,
        Diverged = result.Diverged,
        PlannedChanges = result.PlannedChanges,
        StaleItems = result.StaleItems,
        ManualReviewItems = result.ManualReviewItems.Select(m => new SyncManualReviewItem
        {
            PersonId = m.PersonId.ToString(),
            ElvantoId = m.ElvantoId,
            Reason = m.Reason,
            MatchConfidence = m.MatchConfidence
        }).ToList()
    };
}

/// <summary>
/// By hand: a schedule is a tree, and every list in it is ordered by <c>Position</c>, which Mapperly
/// would neither sort nor carry. The query must include the whole graph - groups, blocks, lanes, items,
/// and the items under sections.
/// </summary>
public class ScheduleConverter : IConverter<DbSchedule, Schedule>
{
    public Schedule Convert(DbSchedule schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        ServiceId = schedule.ServiceId,
        DefaultForServiceTypeId = schedule.DefaultForServiceTypeId,
        WaitingItemId = schedule.WaitingItemId,
        Groups = schedule.Groups
            .OrderBy(g => g.Position)
            .Select(g => new ScheduleGroup { Id = g.Id, Name = g.Name })
            .ToImmutableList(),
        Blocks = schedule.Blocks
            .OrderBy(b => b.Position)
            .Select(b => new ScheduleBlock
            {
                Id = b.Id,
                Lanes = b.Lanes
                    .OrderBy(l => l.Position)
                    .Select(l => new ScheduleLane
                    {
                        Id = l.Id,
                        GroupIds = l.GroupIds.ToImmutableList(),
                        OnScreen = l.OnScreen,
                        Items = l.Items
                            .OrderBy(i => i.Position)
                            .Select(Item)
                            .ToImmutableList()
                    })
                    .ToImmutableList()
            })
            .ToImmutableList()
    };

    private static ScheduleItem Item(DbScheduleItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        FillIn = item.FillIn,
        NeedsFillIn = item.NeedsFillIn,
        ItemTemplateId = item.ItemTemplateId,
        PlannedMinutes = item.PlannedMinutes,
        AdjustmentMinutes = item.AdjustmentMinutes,
        StartsAt = item.StartsAt,
        IsSection = item.IsSection,
        Children = item.Children.OrderBy(c => c.Position).Select(Item).ToImmutableList(),
        ActualStartedAt = item.ActualStartedAt?.UtcDateTime,
        ActualEndedAt = item.ActualEndedAt?.UtcDateTime
    };
}

/// <summary>
/// By hand rather than Mapperly: half the contract is derived - who approved it by name, whether the
/// code was ever exchanged - and the row's credential columns must never be mapped anywhere.
/// <see cref="DbProPresenterMachine.CreatedByUser"/> has to be included by the query for the name.
/// </summary>
public class ProPresenterMachineConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbProPresenterMachine, ProPresenterMachine>
{
    public ProPresenterMachine Convert(DbProPresenterMachine machine) => new()
    {
        Id                    = machine.Id,
        Name                  = machine.Name,
        CreatedAt             = dateTimeConverter.Convert(machine.CreatedAt),
        CreatedBy             = machine.CreatedByUser?.Name,
        Enrolled              = machine.SecretHash != null,
        ProPresenterHost      = machine.ProPresenterHost,
        ProPresenterPort      = machine.ProPresenterPort,
        LastSeenAt            = machine.LastSeenAt.HasValue ? dateTimeConverter.Convert(machine.LastSeenAt.Value) : null,
        AgentVersion          = machine.AgentVersion,
        ProPresenterReachable = machine.ProPresenterReachable,
        ProPresenterVersion   = machine.ProPresenterVersion,
        Timers                = machine.TimersJson == null
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<ProPresenterTimer>>(machine.TimersJson)?.ToImmutableList() ?? [],
        ScheduleTimerUuid     = machine.ScheduleTimerUuid,
        Libraries             = Libraries(machine.LibrariesJson),
        SongLibraryUuids      = SongLibraries(machine.SongLibraryUuidsJson, machine.LibrariesJson)
    };

    public static ImmutableList<ProPresenterLibrary> Libraries(string? librariesJson) =>
        librariesJson == null
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<ProPresenterLibrary>>(librariesJson)?.ToImmutableList() ?? [];

    /// <summary>The libraries the machine copies songs from - the admin page and the agent's configuration both read this.</summary>
    public static ImmutableList<string> SongLibraries(string? songLibraryUuidsJson, string? librariesJson) =>
        ProPresenterLibrary.SongLibraries(
            songLibraryUuidsJson == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<string>>(songLibraryUuidsJson),
            Libraries(librariesJson));
}

/// <summary>By hand rather than Mapperly: the groups and arrangements live in one JSON column.</summary>
public class SongConverter(
    IConverter<DateTimeOffset, DateTime> dateTimeConverter
) : IConverter<DbSong, Song>
{
    public Song Convert(DbSong song)
    {
        SongContent content = SongContent.FromJson(song.ContentJson);

        return new Song
        {
            Id                     = song.Id,
            Name                   = song.Name,
            Library                = song.Library,
            Groups                 = content.Groups,
            Arrangements           = content.Arrangements,
            DefaultArrangementUuid = content.DefaultArrangementUuid,
            UpdatedAt              = dateTimeConverter.Convert(song.UpdatedAt)
        };
    }
}
