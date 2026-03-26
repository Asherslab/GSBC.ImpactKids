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

    [Parameter]
    public bool EnableLoading { get; set; }

    // takes the value of a FirstOrDefault from Filter and feeds it as the first value of this filter
    [Parameter]
    public Func<T, T, bool>? SecondaryFilter { get; set; }

    [Parameter]
    public Func<T, bool>[]? TieredFilters { get; set; }

    [Parameter]
    public string? Search { get; set; }

    [Parameter]
    public int SearchThreshold { get; set; } = 80;

    [Parameter]
    public Func<T, object?>[]? SearchFields { get; set; }

    [Parameter]
    public RenderFragment<Guid?>? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? AdditionalItems { get; set; }

    [Parameter]
    public RenderFragment? NoData { get; set; }

    [Parameter]
    public RenderFragment? NoIds { get; set; }

    [Parameter]
    public EventCallback<bool> NoIdsChanged { get; set; }

    [Parameter]
    public Func<IEnumerable<T>, IEnumerable<T>>? Ordering { get; set; }

    [Parameter]
    public int Spacing { get; set; } = 6;

    [Parameter]
    public int? FakeEntries { get; set; }

    [Parameter]
    public int? Limit { get; set; }

    private bool?                          _noIds;
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
            filteredList = StandardFiltering(filteredList);
        if (TieredFilters != null)
            filteredList = TieredFiltering(filteredList);

        ImmutableList<Guid> ids = filteredList
            .Select(x => x.Id)
            .ToImmutableList();

        bool noIds = ids.Count == 0;

        if (noIds != _noIds)
        {
            _noIds = noIds;
            NoIdsChanged.InvokeAsync(noIds);
        }

        _ids = _ids.ToSuccess(ids);

        StateHasChanged();
    }

    private IEnumerable<T> StandardFiltering(IEnumerable<T> enumerable)
    {
        if (Filter == null)
            return enumerable;

        if (SecondaryFilter == null)
        {
            enumerable = enumerable
                .Where(Filter);
        }
        else
        {
            ImmutableList<T> list = enumerable.ToImmutableList();
            T? filtered = list
                .FirstOrDefault(Filter);

            if (filtered == null)
            {
                _ids = _ids.ToFailure("Filtered Entity Not Found For Secondary Filter");
                StateHasChanged();
                return [];
            }

            enumerable = list
                .Where(x => SecondaryFilter(filtered, x));
        }

        return TieredFilters == null
            ? LimitList(SearchOrderList(enumerable))
            : enumerable;
    }

    // goes through each filter in order, filters it, then searches, orders, and limits it. 
    // if the filtered output is below the limit, do the same for the next filter, until limit reached or filters exhausted
    private IEnumerable<T> TieredFiltering(IEnumerable<T> enumerable)
    {
        if (TieredFilters == null)
            return enumerable;

        ImmutableList<T> list       = enumerable.ToImmutableList();
        ImmutableList<T> outputList = [];

        // Linq query is less readable.
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (Func<T, bool> tieredFilter in TieredFilters)
        {
            // filter original list by 
            ImmutableList<T> filtered = SearchOrderList(
                    list
                        .Where(tieredFilter)
                )
                .ToImmutableList();

            // add this tier's results to the end of the output, excepting any overlaps
            outputList = LimitList(
                    outputList
                        .Concat(filtered.Except(outputList))
                )
                .ToImmutableList();

            if (filtered.Count >= Limit)
                return outputList;
        }

        return outputList;
    }

    private IEnumerable<T> SearchOrderList(IEnumerable<T> list)
    {
        if (Search != null && SearchFields != null)
        {
            list = list
                .FuzzySearch(Search, SearchThreshold, false, null, SearchFields);
        }

        if (Ordering != null)
        {
            list = Ordering(list);
        }


        return list;
    }

    private IEnumerable<T> LimitList(IEnumerable<T> list)
    {
        if (Limit != null)
        {
            list = list.Take(Limit.Value);
        }

        return list;
    }
}