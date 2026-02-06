using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class ReadOnlyDetailsComponent<TEntity> : IDetailsComponent
    where TEntity : IIdentifiable
{
    [Parameter]
    public ModificationState State { get; set; }

    [Parameter]
    public Action<ModificationState>? OnStateChanged { get; set; }

    // noop
    public Task<bool> CreateEntity() => Task.FromResult(false);
    public Task<bool> UpdateEntity() => Task.FromResult(false);
    public Task<bool> DeleteEntity() => Task.FromResult(false);
}