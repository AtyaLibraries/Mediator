using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

/// <summary>
/// Dispatches requests to their registered handlers.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request that returns an untyped result.
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The result returned by the request handler.</returns>
    public ValueTask<Result> Send(IRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that returns an untyped result.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The result returned by the request handler.</returns>
    public ValueTask<Result> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;

    /// <summary>
    /// Sends a request that returns a typed result.
    /// </summary>
    /// <typeparam name="TResponse">The response value type.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The result returned by the request handler.</returns>
    public ValueTask<Result<TResponse>> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that returns a typed result.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response value type.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The result returned by the request handler.</returns>
    public ValueTask<Result<TResponse>> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>;
}
