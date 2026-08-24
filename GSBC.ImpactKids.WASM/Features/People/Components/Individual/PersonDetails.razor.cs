using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDetails
{
    [Parameter]
    public bool BasicView { get; set; }

    [Parameter]
    public Func<bool, Task>? ErrorsChanged { get; set; }

    [Parameter]
    public Guid? FamilyId { get; set; }

    private AsyncData<ImmutableList<FamilyDefinition>>
        _families = AsyncData<ImmutableList<FamilyDefinition>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (FamilyId != null && State == ModificationState.Creating)
            CreateRequest.FamilyId = FamilyId;

        RetrieveFamilies();
        HandleSubscriptionDisposal(EntityStore, _ => RetrieveFamilies());

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }

    /// <summary>
    /// Age of the date of birth currently in the form - the one being edited, not the one
    /// last saved, so it moves as the picker does.
    /// </summary>
    private int? Age() => Person.CalculateAge(
        State == ModificationState.Creating
            ? CreateRequest.LocalDateOfBirth
            : UpdateRequest.LocalDateOfBirth.Value
    );

    /// <summary>Set once the age alone puts them outside Prep to grade 6.</summary>
    private string? AgeWarning()
    {
        int? age = Age();

        return age == null || age >= SchoolGradeTiers.MinimumProgramAge
            ? null
            : $"Under {SchoolGradeTiers.MinimumProgramAge}";
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RetrieveFamilies();
    }

    private void RetrieveFamilies()
    {
        AsyncData<ImmutableList<Person>> people = EntityStore.GetState().Entities;

        if (people.Data == null)
        {
            _families = _families.CopyStatus(people);
            StateHasChanged();
            return;
        }

        ImmutableList<FamilyDefinition> familyDefinitions = people.Data
            .GroupBy(x => x.FamilyId)
            .Select(x =>
                new FamilyDefinition(x.Key,
                    x.GroupBy(y => y.LastName)
                        .MaxBy(y => y.Count())!
                        .Key, // gets the most common last name
                    x.Count()
                )
            )
            .OrderBy(x => x.FamilyName)
            .ThenBy(x => x.FamilyCount)
            .ToImmutableList();

        if (
            FamilyId != null &&
            State == ModificationState.Creating &&
            string.IsNullOrWhiteSpace(CreateRequest.LastName)
        )
        {
            CreateRequest.LastName = familyDefinitions.FirstOrDefault(x => x.Id == FamilyId)?.FamilyName ?? "";
        }

        _families = _families.ToSuccess(familyDefinitions);
        StateHasChanged();
    }
}

public record FamilyDefinition(
    Guid   Id,
    string FamilyName,
    int    FamilyCount
);