using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Multiple;

public partial class MemorisationEntriesTable
{
    [Parameter]
    public Guid? PersonId { get; set; }

    [Parameter]
    public Guid? MemoryVerseId { get; set; }

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Parameter]
    public bool ShowPersonName { get; set; }

    [Parameter]
    public bool ShowMemoryVerse { get; set; }

    [Parameter]
    public bool ShowService { get; set; }

    [Parameter]
    public string[]? SearchStrings { get; set; }

    [Parameter]
    public bool OnlyExisting { get; set; }

    [Parameter]
    public int? Limit { get; set; }

    // the bool is meaningless
    private AsyncData<bool>                   _asyncState = AsyncData<bool>.NotAsked();
    private ImmutableList<MemorisationRecord> _records    = ImmutableList<MemorisationRecord>.Empty;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // HandleSubscriptionDisposal();

        HandleSubscriptionDisposal(MemorisationEntriesStore, _ => RetrieveMemorisationEntries());
        HandleSubscriptionDisposal(PeopleStore, _ => RetrieveMemorisationEntries());
        HandleSubscriptionDisposal(MemoryVersesStore, _ => RetrieveMemorisationEntries());
        HandleSubscriptionDisposal(ServicesStore, _ => RetrieveMemorisationEntries());

        await Task.WhenAll(
            MemorisationEntriesStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            MemoryVersesStore.RefreshAll(),
            ServicesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RetrieveMemorisationEntries();
    }

    private void RetrieveMemorisationEntries()
    {
        if (!SetAsyncDataValues())
            return;

        ImmutableList<Person>            people       = RetrievePeople();
        ImmutableList<Service>           services     = RetrieveServices();
        ImmutableList<MemoryVerseRecord> memoryVerses = RetrieveMemoryVerses();

        List<MemorisationRecord> records = [];

        foreach (Person person in people)
        {
            foreach (Service service in services.OrderBy(x => x.LocalDate))
            {
                foreach (MemoryVerseRecord memoryVerse in memoryVerses)
                {
                    MemorisationRecord? record = CreateMemorisationRecord(records, person, service, memoryVerse);
                    if (record != null)
                        records.Add(record);
                }
            }
        }

        _records = records
            .OrderBy(x => x.Person.FirstName)
            .ThenBy(x => x.Person.LastName)
            .ThenBy(x => x.Service.LocalDate)
            // .ThenBy(x => x.MemoryVerse.Services
            //     .OrderBy(y => y.LocalDate)
            //     .First()
            // )
            .Take(Limit ?? records.Count)
            .ToImmutableList();

        _asyncState = _asyncState.ToSuccess(true);
        StateHasChanged();
    }

    private bool SetAsyncDataValues()
    {
        if (!MemorisationEntriesStore.GetState().Entities.HasData)
        {
            _asyncState = _asyncState.CopyStatus(MemorisationEntriesStore.GetState().Entities);
            return false;
        }

        if (!PeopleStore.GetState().Entities.HasData)
        {
            _asyncState = _asyncState.CopyStatus(PeopleStore.GetState().Entities);
            return false;
        }

        if (!MemoryVersesStore.GetState().Entities.HasData)
        {
            _asyncState = _asyncState.CopyStatus(MemoryVersesStore.GetState().Entities);
            return false;
        }

        // ReSharper disable once InvertIf
        if (!ServicesStore.GetState().Entities.HasData)
        {
            _asyncState = _asyncState.CopyStatus(ServicesStore.GetState().Entities);
            return false;
        }

        return true;
    }

    private MemorisationRecord? CreateMemorisationRecord(
        List<MemorisationRecord> records,
        Person                   person,
        Service                  service,
        MemoryVerseRecord        memoryVerse
    )
    {
        MemorisationEntry? memorisationEntry = MemorisationEntriesStore.GetState().Entities.Data!
            .FirstOrDefault(x =>
                x.PersonId == person.Id &&
                x.ServiceId == service.Id &&
                x.MemoryVerseId == memoryVerse.Id
            );

        bool verseHasBeenSaidBefore = records
            .Any(x => x.Person.Id == PersonId &&
                      x.MemoryVerse.Id == MemoryVerseId && (
                          x.Entry.FiveDollaryDoosGiven ||
                          x.Entry.VerseRecited
                      )
            );

        bool existing = memorisationEntry != null;
        if (memorisationEntry == null && OnlyExisting)
            return null;

        memorisationEntry ??= new MemorisationEntry
        {
            Id = Guid.Empty,

            PersonId = person.Id,
            MemoryVerseId = memoryVerse.Id,
            ServiceId = service.Id,
        };

        return new MemorisationRecord(
            memorisationEntry,
            verseHasBeenSaidBefore,
            person,
            memoryVerse,
            service,
            existing
        );
    }

    private ImmutableList<Person> RetrievePeople()
    {
        IEnumerable<Person> people = PeopleStore.GetState().Entities.Data!.Where(PersonFilter);
        return people.ToImmutableList();
    }

    private bool PersonFilter(Person person)
    {
        if (PersonId != null)
            return person.Id == PersonId;

        if (person.SchoolGrade == null || !SchoolGrades.Contains(person.SchoolGrade.Label))
            return false;

        if (SearchStrings == null)
            return true;

        return SearchStrings.All(x =>
            person.FirstName.Contains(x, StringComparison.InvariantCultureIgnoreCase) ||
            person.LastName.Contains(x, StringComparison.InvariantCultureIgnoreCase)
        );
    }

    private ImmutableList<Service> RetrieveServices()
    {
        IEnumerable<Service> services = ServicesStore.GetState().Entities.Data!.Where(ServiceFilter);
        return services.ToImmutableList();
    }

    private bool ServiceFilter(Service service)
    {
        if (ServiceId != null)
            return service.Id == ServiceId;

        return true;
    }

    private ImmutableList<MemoryVerseRecord> RetrieveMemoryVerses()
    {
        IEnumerable<MemoryVerse> memoryVerses = MemoryVersesStore.GetState().Entities.Data!.Where(MemoryVerseFilter);
        return memoryVerses
            .Select(x => new MemoryVerseRecord
            {
                Services = ServicesStore.GetState().Entities.Data!.Where(y => x.ServiceIds.Contains(y.Id))
                    .ToImmutableList(),
                Id = x.Id,
                ReferenceName = x.ReferenceName,
                Verse = x.Verse,
                MemoryVerseListId = x.MemoryVerseListId,
                ServiceIds = x.ServiceIds,
                BibleVerseIds = x.BibleVerseIds
            })
            .ToImmutableList();
    }

    private bool MemoryVerseFilter(MemoryVerse memoryVerse)
    {
        if (MemoryVerseId != null)
            return memoryVerse.Id == MemoryVerseId;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (ServiceId != null)
            return memoryVerse.ServiceIds.Contains(ServiceId.Value);

        return true;
    }

    private Task CheckboxPressed(
        MemorisationRecord record,
        bool?              recited         = null,
        bool?              fiveDollaryDoos = null,
        bool?              oneDollaryDoo   = null
    )
    {
        if (record.Existing)
        {
            return UpdateMemorisationEntry(
                record.Entry,
                recited,
                fiveDollaryDoos,
                oneDollaryDoo
            );
        }

        return CreateMemorisationEntry(
            record.Entry,
            recited,
            fiveDollaryDoos,
            oneDollaryDoo
        );
    }

    private async Task CreateMemorisationEntry(
        MemorisationEntry entry,
        bool?             recited         = null,
        bool?             fiveDollaryDoos = null,
        bool?             oneDollaryDoo   = null
    )
    {
        _asyncState = _asyncState.ToLoading();
        StateHasChanged();

        CreateMemorisationEntryRequest request = new()
        {
            PersonId = entry.PersonId,
            MemoryVerseId = entry.MemoryVerseId,
            ServiceId = entry.ServiceId,

            VerseRecited = recited ?? false,
            FiveDollaryDoosGiven = fiveDollaryDoos ?? false,
            OneDollaryDooGiven = oneDollaryDoo ?? false
        };

        BasicResponse response = await MemorisationEntriesService.Create(request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            RetrieveMemorisationEntries();
            StateHasChanged();
        }
    }

    private async Task UpdateMemorisationEntry(
        MemorisationEntry entry,
        bool?             recited         = null,
        bool?             fiveDollaryDoos = null,
        bool?             oneDollaryDoo   = null
    )
    {
        _asyncState = _asyncState.ToLoading();
        StateHasChanged();

        UpdateMemorisationEntryRequest request = UpdateMemorisationEntryRequest.FromEntity(entry);

        if (recited != null)
            request.VerseRecited.Value = recited.Value;

        if (fiveDollaryDoos != null)
            request.FiveDollaryDoosGiven.Value = fiveDollaryDoos.Value;

        if (oneDollaryDoo != null)
            request.OneDollaryDooGiven.Value = oneDollaryDoo.Value;

        BasicResponse response = await MemorisationEntriesService.Update(request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            RetrieveMemorisationEntries();
            StateHasChanged();
        }
    }

    private static readonly string[] SchoolGrades =
    [
        "Nursery/Pre-school",
        "Kindergarten",
        "Prep",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6"
    ];
}

public record MemoryVerseRecord : MemoryVerse
{
    public required ImmutableList<Service> Services { get; init; }
}

public record MemorisationRecord(
    MemorisationEntry Entry,
    bool              VerseHasBeenRecitedBefore,
    Person            Person,
    MemoryVerseRecord MemoryVerse,
    Service           Service,
    bool              Existing
);