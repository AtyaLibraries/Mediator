using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

/// <summary>
/// Handles a request that returns an untyped result.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : class, IRequest
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The operation result.</returns>
    public ValueTask<Result> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request that returns a typed result.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response value type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The operation result.</returns>
    public ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken);
}
