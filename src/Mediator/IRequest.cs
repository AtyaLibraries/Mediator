namespace Atya.Application.Mediator;

/// <summary>
/// Marks a request that returns an untyped result.
/// </summary>
public interface IRequest
{
}

/// <summary>
/// Marks a request that returns a typed result.
/// </summary>
/// <typeparam name="TResponse">The response value type.</typeparam>
public interface IRequest<TResponse>
{
}
