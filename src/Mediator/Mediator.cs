using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Atya.Foundation.Guards;
using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

internal sealed class Mediator : IMediator
{
    private readonly FrozenDictionary<Type, IMediatorRequestDispatcher> _requestDispatchers;
    private readonly FrozenDictionary<(Type Request, Type Response), IMediatorResponseDispatcher> _responseDispatchers;

    public Mediator(
        IEnumerable<IMediatorRequestDispatcher> requestDispatchers,
        IEnumerable<IMediatorResponseDispatcher> responseDispatchers)
    {
        Guard.AgainstNull(requestDispatchers);
        Guard.AgainstNull(responseDispatchers);

        _requestDispatchers = BuildRequestDispatchers(requestDispatchers);
        _responseDispatchers = BuildResponseDispatchers(responseDispatchers);
    }

    public ValueTask<Result> Send(IRequest request, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Type requestType = request.GetType();
        if (!_requestDispatchers.TryGetValue(requestType, out IMediatorRequestDispatcher? dispatcher))
        {
            ThrowMissingHandler(requestType);
        }

        return dispatcher.Dispatch(request, cancellationToken);
    }

    public ValueTask<Result> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_requestDispatchers.TryGetValue(typeof(TRequest), out IMediatorRequestDispatcher? dispatcher))
        {
            ThrowMissingHandler(typeof(TRequest));
        }

        return dispatcher.Dispatch(request, cancellationToken);
    }

    public async ValueTask<Result<TResponse>> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Type requestType = request.GetType();
        if (!_responseDispatchers.TryGetValue((requestType, typeof(TResponse)), out IMediatorResponseDispatcher? dispatcher))
        {
            ThrowMissingHandler(requestType);
        }

        object result = await dispatcher.Dispatch(request, cancellationToken).ConfigureAwait(false);

        return (Result<TResponse>)result;
    }

    public async ValueTask<Result<TResponse>> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_responseDispatchers.TryGetValue((typeof(TRequest), typeof(TResponse)), out IMediatorResponseDispatcher? dispatcher))
        {
            ThrowMissingHandler(typeof(TRequest));
        }

        object result = await dispatcher.Dispatch(request, cancellationToken).ConfigureAwait(false);

        return (Result<TResponse>)result;
    }

    private static FrozenDictionary<Type, IMediatorRequestDispatcher> BuildRequestDispatchers(
        IEnumerable<IMediatorRequestDispatcher> dispatchers)
    {
        Dictionary<Type, IMediatorRequestDispatcher> map = new();
        foreach (IMediatorRequestDispatcher dispatcher in dispatchers)
        {
            if (!map.TryAdd(dispatcher.RequestType, dispatcher))
            {
                throw new InvalidOperationException(
                    $"Multiple mediator handlers are registered for '{dispatcher.RequestType.FullName}'.");
            }
        }

        return map.ToFrozenDictionary();
    }

    private static FrozenDictionary<(Type Request, Type Response), IMediatorResponseDispatcher> BuildResponseDispatchers(
        IEnumerable<IMediatorResponseDispatcher> dispatchers)
    {
        Dictionary<(Type Request, Type Response), IMediatorResponseDispatcher> map = new();
        foreach (IMediatorResponseDispatcher dispatcher in dispatchers)
        {
            var key = (dispatcher.RequestType, dispatcher.ResponseType);
            if (!map.TryAdd(key, dispatcher))
            {
                throw new InvalidOperationException(
                    $"Multiple mediator handlers are registered for '{dispatcher.RequestType.FullName}'.");
            }
        }

        return map.ToFrozenDictionary();
    }

    [DoesNotReturn]
    private static void ThrowMissingHandler(Type requestType)
    {
        throw new InvalidOperationException(
            $"No mediator handler is registered for request type '{requestType.Name}'. Register the handler with AddAtyaMediator() source generation or the MediatorRegistrationBuilder escape hatch.");
    }
}
