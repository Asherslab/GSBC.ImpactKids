using Grpc.Net.Client.Web;
using GSBC.ImpactKids.WASM.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using ProtoBuf.Grpc.ClientFactory;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class GrpcServiceExtensions
{
    public static IServiceCollection AddAuthenticatedGrpcClient<T>(
        this IServiceCollection services,
        bool disableRetries = false
    ) where T : class
    {
        return services.AddAuthenticatedGrpcClient<T>(
            new Uri("https://grpc"),
            disableRetries
        );
    }

    private static IServiceCollection AddAuthenticatedGrpcClient<T>(
        this IServiceCollection services,
        Uri                     serviceUri,
        bool disableRetries = false
    )
        where T : class
    {
        IHttpClientBuilder httpClientBuilder = services
            .AddCodeFirstGrpcClient<T>(typeof(T).FullName!, x => { x.Address = serviceUri; })
            .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
            .AddCallCredentials()
            .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(new HttpClientHandler()))
            .AddHttpMessageHandler<UnauthorizedMessageHandler>();

        if (!disableRetries)
            httpClientBuilder.AddStandardResilienceHandler();

        return services;
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
                if (ctx.ServiceUrl.EndsWith("GSBC.ImpactKids.Event") && ctx.MethodName == "Stream") // hard coded exception so that bearer token is added elsewhere
                    return;
                
                Console.WriteLine($"TESTING: {ctx.ServiceUrl} | {ctx.MethodName}");
                
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