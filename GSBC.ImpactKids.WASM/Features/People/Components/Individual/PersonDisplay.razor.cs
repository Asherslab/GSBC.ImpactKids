using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDisplay : ComponentBase
{
    [Parameter]
    public required Person? Person { get; set; }

    [Parameter]
    public bool None { get; set; }

    private string? AvatarDisplay() => None
        ? "N"
        :Person?.FirstName[0].ToString();

    private string DisplayText() => Person == null
        ? "Person"
        : $"{Person.FirstName} {Person.LastName}";

    private Color AvatarColor() => Person == null
        ? Color.Default
        : Person.FamilyGuardian
            ? Color.Secondary
            : Color.Primary;
}