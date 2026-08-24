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

        _filters["All"] = _ => true;
        _filters["Signed In"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id && y.LocalSignedOut == null) != false;
        _filters["Signed Out"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id && y.LocalSignedOut != null) != false;
        _filters["Attended"] = x => _attendanceRecords.Data?
            .Any(y => y.PersonId == x.Id) != false;
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
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Id")
                : _service.ToFailure("Failed to find Service for Today");

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
