using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Multiple;

public partial class PeopleList
{
    [Parameter]
    public Func<Person, bool>? Filter { get; set; }

    [Parameter]
    public Guid? FamilyId { get; set; }

    [Parameter]
    public Guid? FamilyOfId { get; set; }

    private AsyncData<ImmutableList<Guid>> _peopleIds = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        PeopleStore.Subscribe(_ => FilterPeople());

        FilterPeople();
        await Task.WhenAll(
            PeopleStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterPeople();
    }

    private void FilterPeople()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (people.Data == null)
        {
            _peopleIds = _peopleIds.CopyStatus(people);
            return;
        }

        IEnumerable<Person> filteredPeople = people.Data;

        if (Filter != null)
        {
            filteredPeople = filteredPeople
                .Where(Filter);
        }

        if (FamilyId != null || FamilyOfId != null)
        {
            if (FamilyId != null)
            {
                filteredPeople = filteredPeople
                    .Where(x =>
                        x.FamilyId == FamilyId
                    );
            }

            if (FamilyOfId != null)
            {
                Guid? familyId = people.Data.FirstOrDefault(x => x.Id == FamilyOfId)?.FamilyId;
                if (familyId != null)
                {
                    filteredPeople = filteredPeople
                        .Where(x =>
                            x.FamilyId == familyId
                        );
                }
            }

            filteredPeople = filteredPeople
                .OrderByDescending(x => x.FamilyGuardian)
                .ThenBy(x => x.LocalDateOfBirth)
                .ThenBy(x => x.FirstName);
        }

        _peopleIds = _peopleIds.ToSuccess(filteredPeople
            .Select(x => x.Id)
            .ToImmutableList()
        );

        StateHasChanged();
    }
}