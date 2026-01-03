using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDisplay
{
    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Person";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
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