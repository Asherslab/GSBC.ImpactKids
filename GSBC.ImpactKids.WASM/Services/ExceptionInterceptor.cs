using Grpc.Core;
using Grpc.Core.Interceptors;

namespace GSBC.ImpactKids.WASM.Services;

public class ExceptionInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest                                        request,
        ClientInterceptorContext<TRequest, TResponse>   context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation
    )
    {
        AsyncUnaryCall<TResponse> call = continuation(request, context);

        return new AsyncUnaryCall<TResponse>(
            HandleResponse(call.ResponseAsync),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose
        );
    }

    private async Task<TResponse> HandleResponse<TResponse>(Task<TResponse> inner)
    {
        try
        {
            return await inner;
        }
        catch (Exception e)
        {
            switch (e)
            {
                case OperationCanceledException:
                case RpcException
                {
                    StatusCode:
                    StatusCode.Unauthenticated or
                    StatusCode.PermissionDenied or
                    StatusCode.Cancelled
                }:
                    return default!;
                case RpcException rpcException:
                    Console.WriteLine($"RPC ERROR: {rpcException.Status.Detail}");
                    throw;
            }

            throw;
        }
    }
}