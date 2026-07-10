using Atya.Foundation.Guards;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator;

/// <summary>
/// Registers mediator request handlers without runtime assembly scanning.
/// </summary>
/// <remarks>
/// Source-generated registration code calls this builder for each discovered handler. Applications may also
/// call it manually when source generation is disabled or when constructing tests.
/// </remarks>
public sealed class MediatorRegistrationBuilder
{
    private readonly HashSet<(Type Request, Type? Response)> _registrations = new();
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorRegistrationBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public MediatorRegistrationBuilder(IServiceCollection services)
    {
        _services = Guard.AgainstNull(services);
    }

    /// <summary>
    /// Adds a handler for an untyped request.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The current builder.</returns>
    /// <exception cref="InvalidOperationException">A handler is already registered for <typeparamref name="TRequest"/>.</exception>
    public MediatorRegistrationBuilder AddRequestHandler<TRequest, THandler>()
        where TRequest : class, IRequest
        where THandler : class, IRequestHandler<TRequest>
    {
        AddRegistration(typeof(TRequest), responseType: null);
        _services.AddTransient<IRequestHandler<TRequest>, THandler>();
        _services.AddTransient<IMediatorRequestDispatcher, MediatorRequestDispatcher<TRequest>>();

        return this;
    }

    /// <summary>
    /// Adds a handler for a typed request.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response value type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The current builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// A handler is already registered for <typeparamref name="TRequest"/> and <typeparamref name="TResponse"/>.
    /// </exception>
    public MediatorRegistrationBuilder AddRequestHandler<TRequest, TResponse, THandler>()
        where TRequest : class, IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        AddRegistration(typeof(TRequest), typeof(TResponse));
        _services.AddTransient<IRequestHandler<TRequest, TResponse>, THandler>();
        _services.AddTransient<IMediatorResponseDispatcher, MediatorResponseDispatcher<TRequest, TResponse>>();

        return this;
    }

    private void AddRegistration(Type requestType, Type? responseType)
    {
        if (!_registrations.Add((requestType, responseType)))
        {
            string target = responseType is null
                ? requestType.Name
                : $"{requestType.Name} -> {responseType.Name}";

            throw new InvalidOperationException($"A mediator handler is already registered for '{target}'.");
        }
    }
}
