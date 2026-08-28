using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDisplay
{
    [Parameter]
    public bool ShowPersonLink { get; set; }

    [Parameter]
    public string? Link { get; set; }

    [Parameter]
    public bool ShowGrade { get; set; }

    [Parameter]
    public RenderFragment<Person?>? SuffixContent { get; set; }

    /// <summary>
    /// Renders below the person row, spanning the full width of the card. For a control that is
    /// the thing being aimed at rather than a secondary action - it gets the whole tile, and it
    /// sits outside the card's link so pressing it cannot navigate.
    /// </summary>
    [Parameter]
    public RenderFragment<Person?>? FooterContent { get; set; }

    [Parameter]
    public string? Class { get; set; }
    
    private string? Href => ShowPersonLink
        ? Entity.HasData ? $"/People/{Id}" : null
        : Link;

    private string Css => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null)
        .AddClass("person-card-row d-flex justify-start flex-direction-row flex-grow-0")
        .AddClass(Class)
        .Build();

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Person";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            EntityStore.RefreshAll(),
            SchoolGradesStore.RefreshAll()
        );
    }

    private string? GetSchoolGrade()
    {
        if (Entity.Data?.SchoolGradeId == null)
            return null;

        return SchoolGradesStore.GetState().Entities.Data?
            .First(x => x.Id == Entity.Data.SchoolGradeId)
            .Label;
    }

    protected override void OnRetrievedEntity()
    {
        _avatarDisplay = Entity.Data?.FirstName[0].ToString() ?? "N";

        _displayText = Entity.Data == null
            ? "Person"
            : $"{Entity.Data.FirstName} {Entity.Data.LastName}";

        _avatarColor = Entity.Data == null
            ? Color.Default
            : Entity.Data.FamilyGuardian
                ? Color.Secondary
                : Color.Primary;
    }
}