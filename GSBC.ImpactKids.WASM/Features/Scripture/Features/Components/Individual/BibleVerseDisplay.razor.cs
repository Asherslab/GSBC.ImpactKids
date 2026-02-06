using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Components.Individual;

public partial class BibleVerseDisplay
{
    [Parameter]
    public bool ShowDialog { get; set; }

    [Parameter]
    public EventCallback<Guid> OnDelete { get; set; }

    [Parameter]
    public bool AllowDeleting { get; set; }

    private string Class => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", ShowDialog)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-1")
        .Build();

    private async Task OpenDialog()
    {
        if (!ShowDialog)
            return;
        
        await DetailsComponentDialog.Open<BibleVerseDetails>(
            DialogService,
            "Bible Verse",
            ModificationState.Reading,
            Id
        );
    }

    private Task DeleteClicked()
    {
        if (OnDelete.HasDelegate && Entity.Data != null)
            return OnDelete.InvokeAsync(Entity.Data.Id);

        return Task.CompletedTask;
    }
}