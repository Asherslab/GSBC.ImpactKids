using EasyAppDev.Blazor.Store.Blazor.UrlSync;

namespace GSBC.ImpactKids.WASM.Components.Base;

#pragma warning disable EASB001
public class StoreEntityUrlSyncComponent<TEntity> : UrlSyncStoreComponent<TEntity> where TEntity : notnull;
#pragma warning restore EASB001