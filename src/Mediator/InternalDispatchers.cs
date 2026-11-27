using Atya.Foundation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator;

/// <summary>
/// Stateless dispatcher for an untyped request. It resolves the handler from the provider it is given.
/// </summary>
internal abstract class MediatorRequestDispatcher
{
    public abstract ValueTask<Result> Dispatch(IServiceProvider services, object request, CancellationToken cancellationToken);
}

/// <summary>
/// Stateless dispatcher for a typed request.
/// </summary>
internal abstract class MediatorResponseDispatcher
{
    public abstract ValueTask<object> Dispatch(IServiceProvider services, object request, CancellationToken cancellationToken);
}

/// <remarks>
/// Dispatchers resolve the concrete handler type, not the forwarding interface registration: a forwarding factory that
/// returns an already tracked instance would make the container dispose that instance twice.
/// </remarks>
internal sealed class MediatorRequestDispatcher<TRequest, THandler> : MediatorRequestDispatcher
    where TRequest : class, IRequest
    where THandler : class, IRequestHandler<TRequest>
{
    public override ValueTask<Result> Dispatch(IServiceProvider services, object request, CancellationToken cancellationToken) =>
        services.GetRequiredService<THandler>().Handle((TRequest)request, cancellationToken);
}

internal sealed class MediatorResponseDispatcher<TRequest, TResponse, THandler> : MediatorResponseDispatcher
    where TRequest : class, IRequest<TResponse>
    where THandler : class, IRequestHandler<TRequest, TResponse>
{
    public override async ValueTask<object> Dispatch(IServiceProvider services, object request, CancellationToken cancellationToken)
    {
        Result<TResponse> result = await services
            .GetRequiredService<THandler>()
            .Handle((TRequest)request, cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}

/// <summary>A registration produced by <see cref="MediatorRegistrationBuilder"/>.</summary>
internal sealed class MediatorMapEntry
{
    public MediatorMapEntry(Type requestType, Type? responseType, object dispatcher)
    {
        RequestType = requestType;
        ResponseType = responseType;
        Dispatcher = dispatcher;
    }

    public Type RequestType { get; }

    public Type? ResponseType { get; }

    public object Dispatcher { get; }
}
