using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Authentication;
using ProtoBuf.Grpc.ClientFactory;

namespace GSBC.ImpactKids.Web.Extensions;

public static class GrpcServiceExtensions
{
    public static IServiceCollection AddAuthenticatedGrpcClient<T>(
        this IServiceCollection services
    ) where T : class
    {
        return services.AddAuthenticatedGrpcClient<T>(
            new Uri("https://grpc")
        );
    }

    private static IServiceCollection AddAuthenticatedGrpcClient<T>(
        this IServiceCollection services,
        Uri serviceUri
    )
        where T : class
    {
        services
            .AddCodeFirstGrpcClient<T>(typeof(T).FullName!, x => { x.Address = serviceUri; })
            .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
            .AddCallCredentials()
            .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(new HttpClientHandler()));

        return services;
    }

    private static IHttpClientBuilder AddCallCredentials(
        this IHttpClientBuilder builder
    )
    {
        return builder.AddCallCredentials(async (
            _,
            metadata,
            services
        ) =>
        {
            IHttpContextAccessor httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();

            HttpContext httpContext = httpContextAccessor.HttpContext ??
                                      throw new InvalidOperationException(
                                          "No HttpContext available from the IHttpContextAccessor!");

            string? accessToken = await httpContext.GetTokenAsync("id_token");

            if (accessToken != null)
            {
                metadata.Add("Authorization", $"Bearer {accessToken}");
            }
        });
    }
}