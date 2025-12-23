using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Query;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
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
                .AddStoreUtilities()
                .AddEntityStores()
                .AddPageStores();
        }

        private IServiceCollection AddEntityStores()
        {
            services
                .AddEntityStore<ServiceType>();

            return services;
        }
        
        private IServiceCollection AddEntityStore<T>()
        {
            services
                .AddStoreWithUtilities(
                    EntityListState<T>.Initial,
                    (store, sp) => store
                        .WithDefaults(sp, typeof(T).Name)
                )
                .AddScoped<IRefreshableStore<EntityListState<T>>, RefreshableStore<T>>();

            return services;
        }

        private IServiceCollection AddPageStores()
        {
            services.AddStoreWithUtilities(
                MultipleServicesState.Initial,
                (store, sp) => store
                    .WithDefaults(sp, nameof(MultipleServicesState))
            );

            services.AddStoreWithUtilities(
                MultipleServiceTypesState.Initial,
                (store, sp) => store
                    .WithDefaults(sp, nameof(MultipleServiceTypesState))
            );

            return services;
        }
    }
}