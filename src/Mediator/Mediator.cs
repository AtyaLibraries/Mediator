using Atya.Foundation.Guards;
using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

internal sealed class Mediator : IMediator
{
    private readonly Dictionary<Type, IMediatorRequestDispatcher> _requestDispatchers;
    private readonly Dictionary<(Type Request, Type Response), IMediatorResponseDispatcher> _responseDispatchers;

    public Mediator(
        IEnumerable<IMediatorRequestDispatcher> requestDispatchers,
        IEnumerable<IMediatorResponseDispatcher> responseDispatchers)
    {
        Guard.AgainstNull(requestDispatchers);
        Guard.AgainstNull(responseDispatchers);

        _requestDispatchers = BuildRequestDispatchers(requestDispatchers);
        _responseDispatchers = BuildResponseDispatchers(responseDispatchers);
    }

    public ValueTask<Result> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        Guard.AgainstNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_requestDispatchers.TryGetValue(typeof(TRequest), out IMediatorRequestDispatcher? dispatcher))
        {
            return ValueTask.FromResult(Result.Failure(MediatorErrors.HandlerNotRegistered(typeof(TRequest))));
        }

        return dispatcher.Dispatch(request, cancellationToken);
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
            return Result.Failure<TResponse>(MediatorErrors.HandlerNotRegistered(typeof(TRequest)));
        }

        object result = await dispatcher.Dispatch(request, cancellationToken).ConfigureAwait(false);

        return (Result<TResponse>)result;
    }

    private static Dictionary<Type, IMediatorRequestDispatcher> BuildRequestDispatchers(
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

        return map;
    }

    private static Dictionary<(Type Request, Type Response), IMediatorResponseDispatcher> BuildResponseDispatchers(
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

        return map;
    }
}
