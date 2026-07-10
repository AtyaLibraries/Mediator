using System.ComponentModel;
using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

/// <summary>
/// Dispatches an untyped request from generated registration metadata.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMediatorRequestDispatcher
{
    /// <summary>
    /// Gets the request type handled by this dispatcher.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Dispatches the request.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The handler result.</returns>
    public ValueTask<Result> Dispatch(object request, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches a typed request from generated registration metadata.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMediatorResponseDispatcher
{
    /// <summary>
    /// Gets the request type handled by this dispatcher.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets the response value type returned by this dispatcher.
    /// </summary>
    public Type ResponseType { get; }

    /// <summary>
    /// Dispatches the request.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The handler result boxed as a typed result.</returns>
    public ValueTask<object> Dispatch(object request, CancellationToken cancellationToken);
}

internal sealed class MediatorRequestDispatcher<TRequest> : IMediatorRequestDispatcher
    where TRequest : class, IRequest
{
    private readonly IRequestHandler<TRequest> _handler;

    public MediatorRequestDispatcher(IRequestHandler<TRequest> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    public Type RequestType => typeof(TRequest);

    public ValueTask<Result> Dispatch(object request, CancellationToken cancellationToken) =>
        _handler.Handle((TRequest)request, cancellationToken);
}

internal sealed class MediatorResponseDispatcher<TRequest, TResponse> : IMediatorResponseDispatcher
    where TRequest : class, IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _handler;

    public MediatorResponseDispatcher(IRequestHandler<TRequest, TResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    public Type RequestType => typeof(TRequest);

    public Type ResponseType => typeof(TResponse);

    public async ValueTask<object> Dispatch(object request, CancellationToken cancellationToken)
    {
        Result<TResponse> result = await _handler.Handle((TRequest)request, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
