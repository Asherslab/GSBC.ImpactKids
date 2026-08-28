using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

public partial class Tool
{
    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }
    
    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Attendance", href: null, disabled: true),
    ];

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();

    private AsyncData<ImmutableList<AttendanceRecord>> _attendanceRecords =
        AsyncData<ImmutableList<AttendanceRecord>>.NotAsked();

    private string? _search;

    private static readonly Func<Person, object?>[] SearchFields =
    [
        x => $"{x.FirstName} {x.LastName}"
    ];
    
    private readonly Dictionary<string, Func<Person, bool>> _filters = new();

    private readonly Func<Person, bool>[] _tieredFilters;

    public Tool()
    {
        _tieredFilters =
        [
            // TODO: HasAttendedThisYearPersonFilter,
            ProgramGradesPersonFilter,
            EarlyYearsGradesPersonFilter,
            HighSchoolGradesPersonFilter,
            NoSchoolGradesPersonFilter
        ];

        // == true, not != false. Data is null while the store is loading AND when it has
        // failed, and `null != false` is true - so every one of these used to degrade to
        // "show the entire roster" at exactly the moment the leader could not tell.
        // "Signed In" listing 900 names and "Signed In" listing 3 look like the same
        // control working. The Requested filter is the end of night sweep for children
        // asked for and never signed out, so for that one the distinction between "none"
        // and "not loaded" is the whole point of the screen.
        _filters["All"] = _ => true;
        _filters["Signed In"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id && y.LocalSignedOut == null) == true;
        // AwaitingPickup rather than an inline test, so this and the wall can never disagree.
        _filters["Requested"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id && y.AwaitingPickup) == true;
        _filters["Signed Out"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id && y.LocalSignedOut != null) == true;
        _filters["Attended"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id) == true;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ServicesStore, RetrieveService);
        HandleSubscriptionDisposal(SchoolGradesStore, RetrieveSchoolGradeIds);
        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecords);

        RetrieveService();
        RetrieveSchoolGradeIds();

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            SchoolGradesStore.RefreshAll(),
            AttendanceRecordsStore.RefreshAll()
        );
    }

    private void RetrieveService()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            StateHasChanged();
            return;
        }

        Service? service;

        if (ServiceId != null)
        {
            service = services.Data!
                .FirstOrDefault(x => x.Id == ServiceId);
        }
        else
        {
            service = services.Data!
                .FirstOrDefault(x => x.LocalDate.Date == DateTime.Today);

            service ??= services.Data!
                .OrderByDescending(x => x.LocalDate.Date)
                .FirstOrDefault();
        }

        _service = service != null
            ? _service.ToSuccess(service)
            // No ServiceId means we looked today up by date, so say so. These two were the
            // wrong way round, and this is the message a leader reads when the night will
            // not load - it pointed at the wrong problem.
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Today")
                : _service.ToFailure("Failed to find Service for Id");

        RetrieveAttendanceRecords();
        StateHasChanged();
    }

    private void RetrieveAttendanceRecords()
    {
        AsyncData<ImmutableList<AttendanceRecord>> attendanceRecords = AttendanceRecordsStore.GetState().Entities;

        if (attendanceRecords.Data == null)
        {
            _attendanceRecords = _attendanceRecords.CopyStatus(attendanceRecords);
            StateHasChanged();
            return;
        }

        if (_service.Data == null)
        {
            _attendanceRecords = _attendanceRecords.CopyStatus(_service);
            StateHasChanged();
            return;
        }

        ImmutableList<AttendanceRecord> records = attendanceRecords.Data
            .Where(x =>
                !x.Deleted &&
                x.ServiceId == _service.Data.Id
            )
            .ToImmutableList();

        _attendanceRecords = _attendanceRecords.ToSuccess(records);
        StateHasChanged();
    }

    /// <summary>
    /// Prep to grade 6 in one tier - the junior program takes five year olds, so a child
    /// still labelled Kindergarten or Pre-school who has turned five belongs here too,
    /// which is why this one asks <see cref="SchoolGradeTiers"/> instead of matching ids.
    /// </summary>
    private bool ProgramGradesPersonFilter(Person person)
    {
        ImmutableList<SchoolGrade>? grades = SchoolGradesStore.GetState().Entities.Data;

        if (grades == null)
            return true;

        return SchoolGradeTiers.IsInProgram(person, grades);
    }

    /// <summary>Below Prep and not yet five - the ones the program has not reached.</summary>
    private bool EarlyYearsGradesPersonFilter(Person person)
    {
        RetrieveSchoolGradeIds();
        if (_earlyYearsSchoolGradeIds == null)
            return true;

        return person.SchoolGradeId != null &&
               _earlyYearsSchoolGradeIds.Contains(person.SchoolGradeId.Value);
    }

    private bool HighSchoolGradesPersonFilter(Person person)
    {
        RetrieveSchoolGradeIds();
        if (_highSchoolSchoolGradeIds == null)
            return true;

        return person.SchoolGradeId != null &&
               _highSchoolSchoolGradeIds.Contains(person.SchoolGradeId.Value);
    }

    private bool NoSchoolGradesPersonFilter(Person person)
    {
        RetrieveSchoolGradeIds();
        return person.SchoolGradeId == null;
    }

    private void OnSearchChanged(string? search)
    {
        _search = search;
    }

    private void RetrieveSchoolGradeIds()
    {
        if (
            (_earlyYearsSchoolGradeIds != null &&
             _highSchoolSchoolGradeIds != null) ||
            SchoolGradesStore.GetState().Entities.Data == null
        )
            return;

        _earlyYearsSchoolGradeIds = SchoolGradesStore.GetState().Entities.Data?
            .Where(x => SchoolGradeTiers.EarlyYears.Contains(x.Label))
            .Select(x => x.Id)
            .ToArray();

        _highSchoolSchoolGradeIds = SchoolGradesStore.GetState().Entities.Data?
            .Where(x => SchoolGradeTiers.HighSchool.Contains(x.Label))
            .Select(x => x.Id)
            .ToArray();

        StateHasChanged();
    }

    private Guid[]? _earlyYearsSchoolGradeIds;
    private Guid[]? _highSchoolSchoolGradeIds;
}
