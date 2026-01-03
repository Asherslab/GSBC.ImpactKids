using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Multiple;

public partial class ListComponent<T> where T : IIdentifiable
{
    [Parameter]
    public Func<T, bool>? Filter { get; set; }

    // takes the value of a FirstOrDefault from Filter and feeds it as the first value of this filter
    [Parameter]
    public Func<T, T, bool>? SecondaryFilter { get; set; }

    [Parameter]
    public RenderFragment<Guid?>? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? NoData { get; set; }

    [Parameter]
    public RenderFragment? NoIds { get; set; }

    [Parameter]
    public Func<IEnumerable<T>, IEnumerable<T>>? Ordering { get; set; }

    [Parameter]
    public int Spacing { get; set; } = 6;
    
    [Parameter]
    public int? FakeEntries { get; set; }

    private AsyncData<ImmutableList<Guid>> _ids = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Store.Subscribe(_ => FilterList());

        FilterList();
        await Task.WhenAll(
            Store.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterList();
    }

    private void FilterList()
    {
        AsyncData<ImmutableList<T>> list = Store.GetState().Entities;

        if (list.Data == null)
        {
            _ids = _ids.CopyStatus(list);
            StateHasChanged();
            return;
        }

        IEnumerable<T> filteredList = list.Data;

        if (Filter != null)
        {
            if (SecondaryFilter == null)
            {
                filteredList = filteredList
                    .Where(Filter);
            }
            else
            {
                T? filtered = filteredList
                    .FirstOrDefault(Filter);

                if (filtered == null)
                {
                    _ids = _ids.ToFailure("Filtered Entity Not Found For Secondary Filter");
                    StateHasChanged();
                    return;
                }

                filteredList = filteredList
                    .Where(x => SecondaryFilter(filtered, x));
            }
        }

        if (Ordering != null)
        {
            filteredList = Ordering(filteredList);
        }

        _ids = _ids.ToSuccess(filteredList
            .Select(x => x.Id)
            .ToImmutableList()
        );
        StateHasChanged();
    }
}