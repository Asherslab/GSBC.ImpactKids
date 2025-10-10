using System.Reflection;
using GSBC.ImpactKids.Grpc.Conversion;

namespace GSBC.ImpactKids.Grpc.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddConverters(this IServiceCollection services)
    {
        List<Type> converters = new();
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
}