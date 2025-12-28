using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Query;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Features.Calendar;
using GSBC.ImpactKids.WASM.Features.Eventing;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class StateStoreExtensions
{
    extension(IServiceCollection services)
    {
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddStores()
        {
            return services
                .AddQueryClient()
                // .AddStoreUtilities() adds scoped cache. don't want that
                .AddScoped<IDebounceManager, DebounceManager>()
                .AddScoped<IThrottleManager, ThrottleManager>()
                .AddSingleton<SessionStorageProvider>()
                .AddSingleton<ILazyCache, LazyCache>()
                .AddEntityStores()
                .AddComponentStores()
                .AddPageStores();
        }

        private IServiceCollection AddEntityStores()
        {
            return services
                .AddEntityStore<Service>()
                .AddEntityStore<ServiceType>()
                .AddEntityStore<SchoolTerm>()
                .AddEntityStore<DollarStoreEntry>()
                .AddEntityStore<MemoryVerse>();
        }

        private IServiceCollection AddComponentStores()
        {
            // remember, don't make reusable component scoped stores
            // stores are not scoped to a component. EVERY component will update if a single store updates!
            return services;
        }

        private IServiceCollection AddPageStores()
        {
            return services
                .AddPageStore<MultipleServicesState>()
                .AddPageStore<MultipleServiceTypesState>()
                .AddPageStore<EventsStreamState>()
                .AddPageStore<CalendarState>();
        }

        private IServiceCollection AddEntityStore<T>() where T : notnull
        {
            return services
                .AddStore(
                    EntityListState<T>.Initial,
                    (store, sp) => store
                        .WithDefaults(sp, typeof(T).Name)
                )
                .AddSingleton<IAsyncActionExecutor<EntityListState<T>>, AsyncActionExecutor<EntityListState<T>>>()
                .AddSingleton<IRefreshableStore<T>, RefreshableStore<T>>();
        }

        private IServiceCollection AddPageStore<T>() where T : IInitialisableState<T>
        {
            return services
                .AddStore(
                    T.Initial,
                    (store, sp) => store
                        .WithDefaults(sp, typeof(T).Name)
                );
        }
    }
}