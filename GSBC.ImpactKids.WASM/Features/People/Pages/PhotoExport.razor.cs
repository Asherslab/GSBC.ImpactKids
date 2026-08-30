using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

/// <summary>
/// The admin page behind the photo export.
///
/// It only counts and links: the zip itself is a plain anchor to the API on the same origin, so the
/// browser's own download machinery handles it, the existing cookie is attached automatically, and
/// nothing is buffered in the client.
/// </summary>
public partial class PhotoExport : ComponentBase, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>
    /// Null until the store has answered. The page says nothing about counts until then rather than
    /// showing "0 people have a photo", which reads as a real answer while it is still loading.
    /// </summary>
    private int? _countedPhotos;

    protected override async Task OnInitializedAsync()
    {
        _subscriptions.Add(PeopleStore.Subscribe(_ => Recount()));

        Recount();
        await PeopleStore.RefreshAll();
    }

    private void Recount()
    {
        _countedPhotos = PeopleStore.GetState().Entities.Data?
            .Count(x => x.PhotoVersion != null);

        StateHasChanged();
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        GC.SuppressFinalize(this);
    }
}
