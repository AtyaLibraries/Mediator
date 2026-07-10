using Atya.Foundation.Results;

namespace Atya.Application.Mediator;

/// <summary>
/// Creates stable errors returned by the mediator runtime.
/// </summary>
public static class MediatorErrors
{
    /// <summary>
    /// Gets the error code returned when no handler is registered for a request.
    /// </summary>
    public const string HandlerNotRegisteredCode = "atya.application.mediator.handler_not_registered";

    /// <summary>
    /// Creates an error for a request that has no registered handler.
    /// </summary>
    /// <param name="requestType">The unhandled request type.</param>
    /// <returns>A not-found error describing the missing handler registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestType"/> is <see langword="null"/>.</exception>
    public static Error HandlerNotRegistered(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return new Error(
            HandlerNotRegisteredCode,
            $"No mediator handler is registered for request type '{requestType.FullName}'.",
            requestType.FullName,
            kind: ErrorKind.NotFound);
    }
}
