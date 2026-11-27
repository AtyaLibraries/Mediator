using System.Collections.Frozen;

namespace Atya.Application.Mediator;

/// <summary>
/// An immutable lookup from request type to stateless dispatcher. It is a singleton and holds no handler instances.
/// </summary>
internal sealed class MediatorMap
{
    private readonly FrozenDictionary<Type, MediatorRequestDispatcher> _requestDispatchers;
    private readonly FrozenDictionary<(Type Request, Type Response), MediatorResponseDispatcher> _responseDispatchers;

    public MediatorMap(IEnumerable<MediatorMapEntry> entries)
    {
        Dictionary<Type, MediatorRequestDispatcher> requests = new();
        Dictionary<(Type Request, Type Response), MediatorResponseDispatcher> responses = new();

        foreach (MediatorMapEntry entry in entries)
        {
            bool added = entry.ResponseType is null
                ? requests.TryAdd(entry.RequestType, (MediatorRequestDispatcher)entry.Dispatcher)
                : responses.TryAdd((entry.RequestType, entry.ResponseType), (MediatorResponseDispatcher)entry.Dispatcher);

            if (!added)
            {
                throw new InvalidOperationException(
                    $"Multiple mediator handlers are registered for '{entry.RequestType.FullName}'.");
            }
        }

        _requestDispatchers = requests.ToFrozenDictionary();
        _responseDispatchers = responses.ToFrozenDictionary();
    }

    public bool TryGetRequestDispatcher(Type requestType, out MediatorRequestDispatcher dispatcher) =>
        _requestDispatchers.TryGetValue(requestType, out dispatcher!);

    public bool TryGetResponseDispatcher(Type requestType, Type responseType, out MediatorResponseDispatcher dispatcher) =>
        _responseDispatchers.TryGetValue((requestType, responseType), out dispatcher!);
}
