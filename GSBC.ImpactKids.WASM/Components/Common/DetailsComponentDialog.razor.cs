using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Common;

public partial class DetailsComponentDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public required Type DetailsComponentType { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public Dictionary<string, object?>? ExtraParameters { get; set; }

    private          DynamicComponent?           _component;
    private readonly Dictionary<string, object?> _parameters = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _parameters[nameof(IDetailsComponent.State)] = State;
        _parameters[nameof(IDetailsComponent.Id)] = Id;
        if (ExtraParameters != null)
        {
            foreach (KeyValuePair<string, object?> keyValuePair in ExtraParameters)
            {
                _parameters[keyValuePair.Key] = keyValuePair.Value;
            }
        }
    }

    private string ButtonText => State switch
    {
        ModificationState.Creating => "Create",
        ModificationState.Updating => "Update",
        ModificationState.Reading  => "Close",
        _                          => "Error"
    };

    private async Task OnClick()
    {
        if (_component?.Instance is IDetailsComponent detailsComponent)
        {
            switch (State)
            {
                case ModificationState.Creating:
                    await detailsComponent.CreateEntity();
                    break;
                case ModificationState.Updating:
                    await detailsComponent.UpdateEntity();
                    break;
                case ModificationState.Reading:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MudDialog.Close();
        }
    }

    public static async Task<DialogResult?> Open<TComponent>(
        IDialogService               dialogService,
        string                       title,
        ModificationState            state,
        Guid?                        id              = null,
        Dictionary<string, object?>? extraParameters = null
    )
    {
        DialogParameters<DetailsComponentDialog> parameters = new()
        {
            { x => x.DetailsComponentType, typeof(TComponent) },
            { x => x.State, state },
            { x => x.Id, id },
            { x => x.ExtraParameters, extraParameters }
        };
        DialogOptions options = new()
        {
            FullWidth = true
        };
        IDialogReference reference = await dialogService.ShowAsync<DetailsComponentDialog>(title, parameters, options);
        return await reference.Result;
    }
}