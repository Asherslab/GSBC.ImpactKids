using Grpc.Net.Client.Web;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Authentication;
using GSBC.ImpactKids.WASM.Services;
using ProtoBuf.Grpc.ClientFactory;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class GrpcServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthenticatedGrpcClient<T>() where T : class
        {
            return services.AddAuthenticatedGrpcClient<T>(
                new Uri("https://yarp")
            );
        }

        private IServiceCollection AddAuthenticatedGrpcClient<T>(
            Uri serviceUri
        )
            where T : class
        {
            Type serviceType = typeof(T);
            services
                .AddCodeFirstGrpcClient<T>(serviceType.FullName!, x => { x.Address = serviceUri; })
                .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
                .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(new HttpClientHandler()))
                .AddInterceptor<ExceptionInterceptor>();
                // .AddHttpMessageHandler<UnauthorizedMessageHandler>();

            Type? readMultipleServiceBase = serviceType.IsAssignableToGenericType(typeof(IBasicReadMultipleService<>));
            if (readMultipleServiceBase != null)
                services.AddScoped(readMultipleServiceBase, sp => sp.GetRequiredService<T>());

            Type? createServiceBase = serviceType.IsAssignableToGenericType(typeof(ICreateService<>));
            if (createServiceBase != null)
                services.AddScoped(createServiceBase, sp => sp.GetRequiredService<T>());

            Type? updateServiceBase = serviceType.IsAssignableToGenericType(typeof(IUpdateService<>));
            if (updateServiceBase != null)
                services.AddScoped(updateServiceBase, sp => sp.GetRequiredService<T>());

            Type? deleteServiceBase = serviceType.IsAssignableToGenericType(typeof(IBasicDeleteService<>));
            if (deleteServiceBase != null)
                services.AddScoped(deleteServiceBase, sp => sp.GetRequiredService<T>());

            Type? multipleRelationshipsBase =
                serviceType.IsAssignableToGenericType(typeof(IBasicMultipleRelationshipService<,>));
            if (multipleRelationshipsBase != null)
                services.AddScoped(multipleRelationshipsBase, sp => sp.GetRequiredService<T>());

            return services;
        }
    }

    extension(Type givenType)
    {
        private Type? IsAssignableToGenericType(Type genericType)
        {
            Type[] interfaceTypes = givenType.GetInterfaces();

            Type? interfaceType =
                interfaceTypes.FirstOrDefault(it => it.IsGenericType && it.GetGenericTypeDefinition() == genericType);
            if (interfaceType != null)
            {
                return interfaceType;
            }

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
                return givenType;

            Type? baseType = givenType.BaseType;
            return baseType?.IsAssignableToGenericType(genericType);
        }
    }
}