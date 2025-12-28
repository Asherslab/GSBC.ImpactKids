namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public interface IInitialisableState<T>
{
    static abstract T Initial { get; }
}