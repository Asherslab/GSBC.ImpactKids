using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Query;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Features.Attendance;
using GSBC.ImpactKids.WASM.Features.Authentication;
using GSBC.ImpactKids.WASM.Features.Calendar;
using GSBC.ImpactKids.WASM.Features.Eventing;
using GSBC.ImpactKids.WASM.Features.People;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes;
using GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation;
using GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses;
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
                .AddEntityStore<MemorisationEntry>()
                .AddEntityStore<MemoryVerse>()
                .AddEntityStore<MemoryVerseList>()
                .AddEntityStore<BibleVerse>()
                .AddEntityStore<Person>()
                .AddEntityStore<Allergen>()
                .AddEntityStore<Allergy>()
                .AddEntityStore<MedicalNote>()
                .AddEntityStore<MedicalType>()
                .AddEntityStore<SchoolGrade>()
                .AddEntityStore<AttendanceRecord>()
                .AddEntityStore<AttendanceItemType>()
                .AddEntityStore<AttendanceItemRecord>()
                .AddEntityStore<User>();
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
                .AddPageStore<CalendarState>()
                .AddPageStore<MultiplePeopleState>()
                .AddPageStore<MemorisationToolState>()
                .AddPageStore<AttendanceToolState>()
                .AddPageStore<MultipleMemoryVersesState>()
                .AddPageStore<MultipleUsersState>();
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