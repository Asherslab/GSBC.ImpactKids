using System.Reflection;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

namespace GSBC.ImpactKids.Grpc.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddConverters(this IServiceCollection services)
    {
        List<Type> converters = [];
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            converters.AddRange(
                assembly.GetTypes()
                    .Where(x =>
                        x.IsAssignableTo(typeof(IConverter)) &&
                        x is { IsClass: true, IsAbstract: false }
                    )
            );
        }

        foreach (Type converter in converters)
        {
            foreach (Type interfaceType in converter.GetInterfaces())
            {
                services.AddScoped(interfaceType, converter);
            }
        }

        return services;
    }

    public static IServiceCollection AddPeopleSync(this IServiceCollection services)
    {
        // Singleton: AsyncLocal-based context carrier + interceptor
        services.AddSingleton<ISyncContextAccessor, SyncContextAccessor>();

        // Scoped sync services
        services.AddScoped<IPersonMatcher, PersonMatcher>();
        services.AddScoped<IConflictResolver, ConflictResolver>();
        services.AddScoped<IFieldReconciler, FieldReconciler>();
        services.AddScoped<IElvantoPersonSyncService, ElvantoPersonSyncService>();

        // Auto-register all IFieldSyncDescriptor implementations (mirrors AddConverters pattern)
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in assembly.GetTypes()
                .Where(t => t.IsAssignableTo(typeof(IFieldSyncDescriptor)) &&
                            t is { IsClass: true, IsAbstract: false }))
            {
                services.AddScoped(typeof(IFieldSyncDescriptor), type);
            }
        }

        return services;
    }
}