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

    /// <summary>Whose family the new person joins, when that family may not exist yet.</summary>
    [Parameter]
    public Guid? FamilyWithPersonId { get; set; }

    private AsyncData<ImmutableList<FamilyDefinition>>
        _families = AsyncData<ImmutableList<FamilyDefinition>>.NotAsked();

    /// <summary>Matches the person cards: a guardian is secondary, a child primary.</summary>
    private MudBlazor.Color AvatarColor => Entity.Data == null
        ? MudBlazor.Color.Default
        : Entity.Data.FamilyGuardian
            ? MudBlazor.Color.Secondary
            : MudBlazor.Color.Primary;

    /// <summary>
    /// Whether the full-screen capture view is open for this person.
    ///
    /// The same <see cref="PhotoCapture"/> the Photos tool uses, so both routes crop, downscale and
    /// upload identically — a photo taken here is byte-for-byte what one taken during sign-in would
    /// have been, and there is no second upload path to keep in step.
    /// </summary>
    private bool _capturing;

    private void OpenCapture() => _capturing = true;

    private void CloseCapture() => _capturing = false;

    /// <summary>
    /// The upload has already raised the person update event, so the store refreshes itself and the
    /// avatar picks up the new version on its own — re-fetching here would only race that. Closing
    /// is all that is left.
    /// </summary>
    private void OnPhotoSaved(string photoVersion) => _capturing = false;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (FamilyId != null && State == ModificationState.Creating)
            CreateRequest.FamilyId = FamilyId;

        if (FamilyWithPersonId != null && State == ModificationState.Creating)
            CreateRequest.FamilyWithPersonId = FamilyWithPersonId;

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

        // People with no household are excluded rather than grouped. They all share Guid.Empty, so
        // grouping would offer "no family" in the picker as though it were a family - the same shape
        // as the old bucket, which listed itself as "Kent (412)".
        ImmutableList<FamilyDefinition> familyDefinitions = people.Data
            .Where(x => x.HasFamily)
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

        if (State == ModificationState.Creating && string.IsNullOrWhiteSpace(CreateRequest.LastName))
        {
            // From the family when there is one, otherwise from the person whose family is about to
            // be created - they are the only surname on offer, and it is the same convenience the
            // family case has always had.
            if (FamilyId != null)
                CreateRequest.LastName = familyDefinitions.FirstOrDefault(x => x.Id == FamilyId)?.FamilyName ?? "";
            else if (FamilyWithPersonId != null)
                CreateRequest.LastName =
                    people.Data.FirstOrDefault(x => x.Id == FamilyWithPersonId)?.LastName ?? "";
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