using Atya.Foundation.Guards;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator;

/// <summary>
/// Registers mediator request handlers without runtime assembly scanning.
/// </summary>
/// <remarks>
/// Source-generated registration code calls this builder for each discovered handler interface. Applications may also
/// call it manually when source generation is disabled or when constructing tests. A handler type that implements several
/// handler interfaces is registered once under its concrete type and each interface is forwarded to that registration,
/// so a scoped handler is a single instance per scope across all of its interfaces.
/// </remarks>
public sealed class MediatorRegistrationBuilder
{
    private readonly Dictionary<Type, MediatorHandlerLifetime> _handlerLifetimes = new();
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
    /// <param name="lifetime">The handler lifetime. Defaults to <see cref="MediatorHandlerLifetime.Transient"/>.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// A handler is already registered for <typeparamref name="TRequest"/>, or <typeparamref name="THandler"/> was
    /// already registered with a different lifetime.
    /// </exception>
    public MediatorRegistrationBuilder AddRequestHandler<TRequest, THandler>(
        MediatorHandlerLifetime lifetime = MediatorHandlerLifetime.Transient)
        where TRequest : class, IRequest
        where THandler : class, IRequestHandler<TRequest>
    {
        ServiceLifetime serviceLifetime = ToServiceLifetime(lifetime);
        AddRegistration(typeof(TRequest), responseType: null);
        AddHandler<THandler>(lifetime, serviceLifetime);
        _services.Add(new ServiceDescriptor(
            typeof(IRequestHandler<TRequest>),
            static sp => sp.GetRequiredService<THandler>(),
            serviceLifetime));
        _services.AddSingleton(new MediatorMapEntry(typeof(TRequest), null, new MediatorRequestDispatcher<TRequest, THandler>()));

        return this;
    }

    /// <summary>
    /// Adds a handler for a typed request.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response value type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="lifetime">The handler lifetime. Defaults to <see cref="MediatorHandlerLifetime.Transient"/>.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// A handler is already registered for <typeparamref name="TRequest"/> and <typeparamref name="TResponse"/>, or
    /// <typeparamref name="THandler"/> was already registered with a different lifetime.
    /// </exception>
    public MediatorRegistrationBuilder AddRequestHandler<TRequest, TResponse, THandler>(
        MediatorHandlerLifetime lifetime = MediatorHandlerLifetime.Transient)
        where TRequest : class, IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        ServiceLifetime serviceLifetime = ToServiceLifetime(lifetime);
        AddRegistration(typeof(TRequest), typeof(TResponse));
        AddHandler<THandler>(lifetime, serviceLifetime);
        _services.Add(new ServiceDescriptor(
            typeof(IRequestHandler<TRequest, TResponse>),
            static sp => sp.GetRequiredService<THandler>(),
            serviceLifetime));
        _services.AddSingleton(new MediatorMapEntry(typeof(TRequest), typeof(TResponse), new MediatorResponseDispatcher<TRequest, TResponse, THandler>()));

        return this;
    }

    private static ServiceLifetime ToServiceLifetime(MediatorHandlerLifetime lifetime) => lifetime switch
    {
        MediatorHandlerLifetime.Transient => ServiceLifetime.Transient,
        MediatorHandlerLifetime.Scoped => ServiceLifetime.Scoped,
        MediatorHandlerLifetime.Singleton => ServiceLifetime.Singleton,
        _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unknown handler lifetime."),
    };

    private void AddHandler<THandler>(MediatorHandlerLifetime lifetime, ServiceLifetime serviceLifetime)
        where THandler : class
    {
        if (_handlerLifetimes.TryGetValue(typeof(THandler), out MediatorHandlerLifetime existing))
        {
            if (existing != lifetime)
            {
                throw new InvalidOperationException(
                    $"Mediator handler '{typeof(THandler).Name}' is registered with lifetime {existing} and {lifetime}. A handler type has exactly one lifetime.");
            }

            return;
        }

        _handlerLifetimes.Add(typeof(THandler), lifetime);
        _services.Add(new ServiceDescriptor(typeof(THandler), typeof(THandler), serviceLifetime));
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
