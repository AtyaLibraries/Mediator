using System.Diagnostics.CodeAnalysis;
using Atya.Foundation.Guards;
using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

/// <summary>
/// Scoped mediator. It holds the shared map and the current scope's provider, so resolving it constructs no handlers;
/// each send resolves exactly one handler from the ambient scope, whose container owns its disposal.
/// </summary>
internal sealed class Mediator : IMediator
{
    private readonly MediatorMap _map;
    private readonly IServiceProvider _services;

    public Mediator(MediatorMap map, IServiceProvider services)
    {
        _map = Guard.AgainstNull(map);
        _services = Guard.AgainstNull(services);
    }

    public ValueTask<Result> Send(IRequest request, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Type requestType = request.GetType();
        if (!_map.TryGetRequestDispatcher(requestType, out MediatorRequestDispatcher? dispatcher))
        {
            ThrowMissingHandler(requestType);
        }

        return dispatcher.Dispatch(_services, request, cancellationToken);
    }

    public ValueTask<Result> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_map.TryGetRequestDispatcher(typeof(TRequest), out MediatorRequestDispatcher? dispatcher))
        {
            ThrowMissingHandler(typeof(TRequest));
        }

        return dispatcher.Dispatch(_services, request, cancellationToken);
    }

    public async ValueTask<Result<TResponse>> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Type requestType = request.GetType();
        if (!_map.TryGetResponseDispatcher(requestType, typeof(TResponse), out MediatorResponseDispatcher? dispatcher))
        {
            ThrowMissingHandler(requestType);
        }

        object result = await dispatcher.Dispatch(_services, request, cancellationToken).ConfigureAwait(false);

        return (Result<TResponse>)result;
    }

    public async ValueTask<Result<TResponse>> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_map.TryGetResponseDispatcher(typeof(TRequest), typeof(TResponse), out MediatorResponseDispatcher? dispatcher))
        {
            ThrowMissingHandler(typeof(TRequest));
        }

        object result = await dispatcher.Dispatch(_services, request, cancellationToken).ConfigureAwait(false);

        return (Result<TResponse>)result;
    }

    [DoesNotReturn]
    private static void ThrowMissingHandler(Type requestType)
    {
        throw new InvalidOperationException(
            $"No mediator handler is registered for request type '{requestType.Name}'. Register the handler with AddAtyaMediator() source generation or the MediatorRegistrationBuilder escape hatch.");
    }
}
