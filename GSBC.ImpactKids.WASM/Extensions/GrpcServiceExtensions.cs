using Grpc.Net.Client.Web;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Authentication;
using GSBC.ImpactKids.WASM.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using ProtoBuf.Grpc.ClientFactory;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class GrpcServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthenticatedGrpcClient<T>() where T : class
        {
            return services.AddAuthenticatedGrpcClient<T>(
                new Uri("https://grpc")
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
                .AddCallCredentials()
                .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(new HttpClientHandler()))
                .AddInterceptor<ExceptionInterceptor>()
                .AddHttpMessageHandler<UnauthorizedMessageHandler>();

            Type? readMultipleServiceBase = serviceType.IsAssignableToGenericType(typeof(IBasicReadMultipleService<>));
            if (readMultipleServiceBase != null)
                services.AddScoped(readMultipleServiceBase, sp => sp.GetRequiredService<T>());

            return services;
        }
    }

    public static Type? IsAssignableToGenericType(this Type givenType, Type genericType)
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
        return baseType == null ? null : IsAssignableToGenericType(baseType, genericType);
    }

    private static IHttpClientBuilder AddCallCredentials(
        this IHttpClientBuilder builder
    )
    {
        return builder.AddCallCredentials(async (
            ctx,
            metadata,
            services
        ) =>
        {
            try
            {
                if (ctx.ServiceUrl.EndsWith("GSBC.ImpactKids.Event") &&
                    ctx.MethodName == "Stream") // hard coded exception so that bearer token is added elsewhere
                    return;

                IAccessTokenProvider? authTokenProvider = services.GetService<IAccessTokenProvider>();
                if (authTokenProvider == null)
                    return;

                AccessTokenResult result = await authTokenProvider.RequestAccessToken();
                
                if (!result.TryGetToken(out AccessToken? token))
                    return;

                metadata.Add("Authorization", $"Bearer {token.Value}");
            }
            catch (Exception)
            {
                // ignored
            }
        });
    }
}