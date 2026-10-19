using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using GSBC.ImpactKids.ProPresenterAgent.Credentials;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.ProPresenter;
using Microsoft.Extensions.Options;
using ProtoBuf.Grpc.Client;

namespace GSBC.ImpactKids.ProPresenterAgent.ImpactKids;

/// <summary>
/// The agent's one gRPC client: <see cref="IProPresenterAgentService"/>, over grpc-web through the
/// proxy's <c>machine/</c> route - the same wire format the browser app uses, so it crosses the proxy
/// and Cloudflare exactly as that does.
/// <para>
/// Every call carries a machine access token, fetched or reused per call by <see cref="TokenClient"/>.
/// The credential is set once enrolment has produced one; until then there is nothing to call with.
/// </para>
/// </summary>
public sealed class ImpactKidsClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly TokenClient _tokens;

    private MachineCredential? _credential;

    public ImpactKidsClient(IOptions<AgentOptions> options, TokenClient tokens)
    {
        _tokens = tokens;

        Uri  address = options.Value.ImpactKidsUrl;
        bool secure  = address.Scheme == Uri.UriSchemeHttps;

        CallCredentials bearer = CallCredentials.FromInterceptor(async (context, metadata) =>
        {
            MachineCredential credential = _credential
                ?? throw new InvalidOperationException("No machine credential yet - enrol first");

            string accessToken = await _tokens.GetAccessTokenAsync(credential, context.CancellationToken);

            metadata.Add("Authorization", $"Bearer {accessToken}");
        });

        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()),
            Credentials = ChannelCredentials.Create(
                secure ? ChannelCredentials.SecureSsl : ChannelCredentials.Insecure, bearer),
            // Only ever true against a local http AppHost. Production is https.
            UnsafeUseInsecureChannelCallCredentials = !secure
        });

        Agent = _channel.CreateGrpcService<IProPresenterAgentService>();
    }

    public IProPresenterAgentService Agent { get; }

    /// <summary>
    /// Gets a token outside any gRPC call. An exception from inside the call credentials comes back
    /// wrapped as a generic <see cref="RpcException"/>, which would hide the one answer that matters -
    /// <see cref="CredentialRevokedException"/> - so each round asks here first, where it surfaces as itself.
    /// </summary>
    public async Task EnsureAuthenticatedAsync(CancellationToken token)
    {
        MachineCredential credential = _credential
            ?? throw new InvalidOperationException("No machine credential yet - enrol first");

        await _tokens.GetAccessTokenAsync(credential, token);
    }

    public void UseCredential(MachineCredential? credential)
    {
        _credential = credential;
        _tokens.Forget();
    }

    public void Dispose() => _channel.Dispose();
}
